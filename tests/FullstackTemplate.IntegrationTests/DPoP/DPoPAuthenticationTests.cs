namespace FullstackTemplate.IntegrationTests.DPoP;

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Shouldly;

/// <summary>
/// One test for each rule of RFC 9449 section 4.3, and for the binding rules of sections 7.1 and 7.2. Each test starts
/// from a request that passes, breaks exactly one rule, and expects a 401 with the error of section 7.1. Thus a test
/// fails if the check for its rule is missing.
/// </summary>
public class DPoPAuthenticationTests : TestBase
{
    private const string InvalidProof = "invalid_dpop_proof";
    private const string InvalidToken = "invalid_token";

    /// <summary>
    /// A request that passes all the checks, and the proof builder to break one rule.
    /// </summary>
    private sealed class Case : IAsyncDisposable
    {
        public required DPoPTestHost Host { get; init; }
        public required DPoPProofBuilder Proof { get; init; }
        public required string AccessToken { get; set; }
        public string Scheme { get; set; } = "DPoP";
        public HttpMethod Method { get; set; } = HttpMethod.Get;
        public string Path { get; set; } = DPoPTestHost.ProtectedPath;

        public static async Task<Case> CreateAsync()
        {
            var host = await DPoPTestHost.StartAsync();
            var token = DPoPTestHost.AccessToken();
            var proof = new DPoPProofBuilder(token);
            // The access token is bound to the key of the proof
            return new Case { Host = host, Proof = proof, AccessToken = DPoPTestHost.AccessToken(proof.Thumbprint) }
                .Rebuild();
        }

        // The ath of the proof is for the final access token
        public Case Rebuild()
        {
            Proof.Payload["ath"] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(
                SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(AccessToken)));
            return this;
        }

        public Task<HttpResponseMessage> SendAsync(string? proof = null)
        {
            using var request = Host.Request(Scheme, AccessToken, proof ?? Proof.Build(), Method, Path);
            return Host.Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Proof.Dispose();
            await Host.DisposeAsync();
        }
    }

    private static string Challenge(HttpResponseMessage response) =>
        string.Join(" | ", response.Headers.WwwAuthenticate.Select(h => $"{h.Scheme} {h.Parameter}"));

    private static async Task ShouldBeRejectedAsync(HttpResponseMessage response, string error)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var challenge = response.Headers.WwwAuthenticate.Single();
        challenge.Scheme.ShouldBe("DPoP");
        challenge.Parameter.ShouldNotBeNull().ShouldContain($"error=\"{error}\"");
        challenge.Parameter.ShouldContain("algs=\"ES256");
        await Task.CompletedTask;
    }

    [Test]
    public async Task a_valid_proof_with_a_bound_token_passes()
    {
        // Arrange
        await using var c = await Case.CreateAsync();

        // Act
        var response = await c.SendAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_proof_for_a_request_with_a_query_passes_with_an_htu_without_the_query()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Path = DPoPTestHost.ProtectedPath + "?page=2";

        // Act
        var response = await c.SendAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task an_htu_with_the_query_and_the_default_port_and_other_case_of_the_host_passes()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.Payload["htu"] = "http://LOCALHOST:80/protected?x=1#frag";

        // Act
        var response = await c.SendAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Rule 1
    [Test]
    public async Task more_than_one_DPoP_header_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        using var request = c.Host.Request("DPoP", c.AccessToken, c.Proof.Build());
        request.Headers.TryAddWithoutValidation("DPoP", new DPoPProofBuilder(c.AccessToken).Build());

        // Act
        var response = await c.Host.Client.SendAsync(request);

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    [Test]
    public async Task a_missing_DPoP_header_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        using var request = c.Host.Request("DPoP", c.AccessToken, proof: null);

        // Act
        var response = await c.Host.Client.SendAsync(request);

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    // Rule 2
    [Test]
    [Arguments("not-a-jwt")]
    [Arguments("a.b.c")]
    [Arguments("e30.e30")]
    public async Task a_proof_that_is_not_a_well_formed_JWT_is_rejected(string proof)
    {
        // Arrange
        await using var c = await Case.CreateAsync();

        // Act
        var response = await c.SendAsync(proof);

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    // Rule 3
    [Test]
    [Arguments("jti")]
    [Arguments("htm")]
    [Arguments("htu")]
    [Arguments("iat")]
    [Arguments("ath")]
    public async Task a_proof_without_a_required_claim_is_rejected(string claim)
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.Payload.Remove(claim);

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    // Rule 4
    [Test]
    [Arguments("JWT")]
    [Arguments("at+jwt")]
    [Arguments("DPOP+JWT")]
    public async Task a_proof_with_another_typ_is_rejected(string typ)
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.Header["typ"] = typ;

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    [Test]
    public async Task a_proof_without_a_typ_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.Header.Remove("typ");

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    // Rule 5
    [Test]
    [Arguments("none")]
    [Arguments("HS256")]
    [Arguments("ES999")]
    public async Task a_proof_with_an_alg_that_is_not_accepted_is_rejected(string alg)
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.Header["alg"] = alg;

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    // Rule 6
    [Test]
    public async Task a_proof_that_is_signed_with_another_key_than_the_jwk_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.SigningKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    [Test]
    public async Task a_proof_with_a_changed_payload_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        var parts = c.Proof.Build().Split('.');
        var tampered = new DPoPProofBuilder(c.AccessToken);
        tampered.Payload["jti"] = "another";
        var payload = tampered.Build().Split('.')[1];

        // Act
        var response = await c.SendAsync($"{parts[0]}.{payload}.{parts[2]}");

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    [Test]
    public async Task a_proof_without_a_jwk_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.Header.Remove("jwk");

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    // Rule 7
    [Test]
    public async Task a_jwk_with_a_private_key_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        var jwk = DPoPProofBuilder.PublicJwk(c.Proof.Key);
        jwk["d"] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(c.Proof.Key.ExportParameters(true).D);
        c.Proof.Header["jwk"] = jwk;

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    [Test]
    public async Task a_jwk_with_a_private_key_member_in_another_case_is_rejected()
    {
        // Arrange
        // IdentityModel reads the members of a JWK without case, so it reads "D" as the private key "d"
        await using var c = await Case.CreateAsync();
        var jwk = DPoPProofBuilder.PublicJwk(c.Proof.Key);
        jwk["D"] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(c.Proof.Key.ExportParameters(true).D);
        c.Proof.Header["jwk"] = jwk;

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    [Test]
    [Arguments("x5c")]
    [Arguments("x5u")]
    [Arguments("x5t")]
    [Arguments("x5t#S256")]
    [Arguments("X5C")]
    public async Task a_jwk_with_a_member_that_points_to_a_certificate_is_rejected(string member)
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        var jwk = DPoPProofBuilder.PublicJwk(c.Proof.Key);
        jwk[member] = member.Equals("x5c", StringComparison.OrdinalIgnoreCase) ? new[] { "MIIB" } : "value";
        c.Proof.Header["jwk"] = jwk;

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    [Test]
    public async Task a_jwk_with_the_n_and_e_of_the_bound_key_and_the_x5c_of_another_key_is_rejected()
    {
        // Arrange
        // The attack: the thumbprint comes from n and e, so it matches the access token. A signature check that
        // uses the certificate of x5c lets the attacker sign with a key of their own.
        await using var c = await Case.CreateAsync();
        using var victim = RSA.Create(2048);
        var attacker = RSA.Create(2048);
        using var certificate = new CertificateRequest("CN=attacker", attacker, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DPoPTestHost.Now.AddDays(-1), DPoPTestHost.Now.AddDays(1));
        var victimKey = victim.ExportParameters(false);
        var victimJwk = new Dictionary<string, object?>
        {
            ["kty"] = "RSA",
            ["n"] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(victimKey.Modulus),
            ["e"] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(victimKey.Exponent),
        };
        c.AccessToken = DPoPTestHost.AccessToken(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(
            new Microsoft.IdentityModel.Tokens.JsonWebKey(JsonSerializer.Serialize(victimJwk)).ComputeJwkThumbprint()));
        c.Rebuild();
        c.Proof.Header["alg"] = "RS256";
        c.Proof.Header["jwk"] = new Dictionary<string, object?>(victimJwk) { ["x5c"] = new[] { Convert.ToBase64String(certificate.RawData) } };
        c.Proof.RsaSigningKey = attacker;

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    [Test]
    public async Task a_proof_with_an_RSA_jwk_passes()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        var key = RSA.Create(2048);
        var parameters = key.ExportParameters(false);
        var jwk = new Dictionary<string, object?>
        {
            ["kty"] = "RSA",
            ["n"] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(parameters.Modulus),
            ["e"] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(parameters.Exponent),
        };
        c.AccessToken = DPoPTestHost.AccessToken(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(
            new Microsoft.IdentityModel.Tokens.JsonWebKey(JsonSerializer.Serialize(jwk)).ComputeJwkThumbprint()));
        c.Rebuild();
        c.Proof.Header["alg"] = "RS256";
        c.Proof.Header["jwk"] = jwk;
        c.Proof.RsaSigningKey = key;

        // Act
        var response = await c.SendAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Rule 8
    [Test]
    public async Task an_htm_that_is_not_the_method_of_the_request_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.Payload["htm"] = "POST";

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    // Rule 9
    [Test]
    [Arguments("http://localhost/other")]
    [Arguments("http://example.com/protected")]
    [Arguments("https://localhost/protected")]
    [Arguments("not a url")]
    public async Task an_htu_that_is_not_the_url_of_the_request_is_rejected(string htu)
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.Payload["htu"] = htu;

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    // Rule 11
    [Test]
    public async Task an_iat_that_is_too_old_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.Payload["iat"] = DPoPTestHost.Now.AddSeconds(-61).ToUnixTimeSeconds();

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    [Test]
    public async Task an_iat_in_the_future_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.Payload["iat"] = DPoPTestHost.Now.AddSeconds(30).ToUnixTimeSeconds();

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    [Test]
    public async Task an_iat_that_is_not_a_number_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.Payload["iat"] = "now";

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    // Rule 12
    [Test]
    public async Task an_ath_that_is_not_the_hash_of_the_access_token_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.Payload["ath"] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(SHA256.HashData([1, 2, 3]));

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidProof);
    }

    [Test]
    public async Task a_proof_with_another_key_than_the_cnf_jkt_of_the_access_token_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        using var otherKey = new DPoPProofBuilder("unused");
        c.AccessToken = DPoPTestHost.AccessToken(otherKey.Thumbprint);
        c.Rebuild();

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidToken);
    }

    // The replay of jti
    [Test]
    public async Task a_proof_that_was_used_before_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        var proof = c.Proof.Build();

        // Act
        var first = await c.SendAsync(proof);
        var second = await c.SendAsync(proof);

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ShouldBeRejectedAsync(second, InvalidProof);
    }

    [Test]
    public async Task a_proof_that_fails_another_check_does_not_use_its_jti()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Proof.Payload["htm"] = "POST";
        var wrongMethod = c.Proof.Build();
        c.Proof.Payload["htm"] = "GET";
        var right = c.Proof.Build();

        // Act
        var failed = await c.SendAsync(wrongMethod);
        var passed = await c.SendAsync(right);

        // Assert
        failed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        passed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_replay_after_the_replay_window_fails_the_iat_check_and_a_new_proof_passes()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        var proof = c.Proof.Build();
        (await c.SendAsync(proof)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        // After the window the replay cache can forget the jti, so the iat check must reject the old proof
        c.Host.Time.Advance(new Server.Resources.Entrypoints.DPoP.DPoPOptions().ReplayWindow + TimeSpan.FromSeconds(1));
        var replay = await c.SendAsync(proof);
        c.Proof.Payload["jti"] = Guid.NewGuid().ToString("N");
        c.Proof.Payload["iat"] = c.Host.Time.GetUtcNow().ToUnixTimeSeconds();
        var next = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(replay, InvalidProof);
        Challenge(replay).ShouldContain("iat");
        next.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Section 7.2
    [Test]
    public async Task a_DPoP_bound_token_sent_as_a_bearer_token_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Scheme = "Bearer";

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidToken);
    }

    // Section 7.1
    [Test]
    public async Task an_access_token_without_a_cnf_jkt_sent_with_the_DPoP_scheme_is_rejected()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.AccessToken = DPoPTestHost.AccessToken();
        c.Rebuild();

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidToken);
    }

    [Test]
    public async Task a_DPoP_bound_token_that_the_handler_gives_as_another_token_type_is_rejected()
    {
        // Arrange
        // The legacy validators give a JwtSecurityToken. The code cannot read its cnf.jkt, so it must not let the
        // token pass as a bearer token.
        await using var host = await DPoPTestHost.StartAsync(useSecurityTokenValidators: true);
        using var proof = new DPoPProofBuilder("unused");
        using var request = host.Request("Bearer", DPoPTestHost.AccessToken(proof.Thumbprint), proof: null);

        // Act
        var response = await host.Client.SendAsync(request);

        // Assert
        await ShouldBeRejectedAsync(response, InvalidToken);
    }

    [Test]
    public async Task a_token_that_is_not_bound_still_works_as_a_bearer_token()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.Scheme = "Bearer";
        c.AccessToken = DPoPTestHost.AccessToken();

        using var request = c.Host.Request("Bearer", c.AccessToken, proof: null);

        // Act
        var response = await c.Host.Client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_request_with_no_credentials_gets_a_Bearer_and_a_DPoP_challenge()
    {
        // Arrange
        await using var host = await DPoPTestHost.StartAsync();

        // Act
        var response = await host.Client.GetAsync(DPoPTestHost.BaseUrl + DPoPTestHost.ProtectedPath);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var challenge = Challenge(response);
        challenge.ShouldContain("Bearer");
        challenge.ShouldContain("DPoP algs=\"ES256");
    }

    [Test]
    public async Task a_DPoP_scheme_request_with_an_invalid_access_token_gets_a_DPoP_invalid_token_challenge()
    {
        // Arrange
        await using var c = await Case.CreateAsync();
        c.AccessToken = "not.a.token";

        // Act
        var response = await c.SendAsync();

        // Assert
        await ShouldBeRejectedAsync(response, InvalidToken);
    }
}
