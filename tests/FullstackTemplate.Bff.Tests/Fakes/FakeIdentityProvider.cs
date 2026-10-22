namespace FullstackTemplate.Bff.Tests.Fakes;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// A minimal OpenID Connect provider on a real port, so that the OIDC handler and token management of the BFF
/// talk to it over HTTP as they talk to a real provider.
/// <para>
/// The authorize endpoint reads test-only query parameters, so that each test chooses its own user:
/// <c>test_sub</c>, <c>test_sid</c>, <c>test_expires_in</c> (<c>omit</c> for no <c>expires_in</c> in each token
/// response), <c>test_refresh_expires_in</c> (the <c>expires_in</c> of a refresh response, by default
/// <c>test_expires_in</c>), <c>test_session_state</c>, <c>test_error</c>,
/// <c>test_refresh</c> (<c>fail</c> makes each refresh return <c>invalid_grant</c>, and <c>unavailable</c> a 503),
/// <c>test_revoke</c> (<c>fail</c> makes each revocation of a refresh token of the user return 500, and <c>drop</c>
/// closes the connection),
/// <c>test_iss</c> (the <c>iss</c> of the callback, or <c>omit</c> for none), <c>test_aud</c> (one more audience in
/// the ID token), <c>test_azp</c> (the <c>azp</c> of the ID token), <c>test_nonce=wrong</c> (an ID token
/// <c>nonce</c> that is not the one of the authorize request), <c>test_at_hash=wrong</c> (an <c>at_hash</c> that
/// is not the hash of the access token), and <c>test_id_token</c> (<c>unsigned</c> gives an ID token with
/// <c>alg=none</c>). The ID token has the correct <c>at_hash</c> by default. By default a refresh
/// response has no <c>id_token</c>. <c>test_refresh_id_token</c> adds one: <c>valid</c>, or one with a fault,
/// <c>wrong_iss</c>, <c>wrong_sub</c>, <c>wrong_aud</c>, <c>wrong_azp</c>, <c>wrong_auth_time</c>, <c>expired</c>, or
/// <c>bad_signature</c> (signed with another key with the <c>kid</c> of the provider key). Refresh tokens rotate:
/// each one works one time, and a reuse gets <c>invalid_grant</c>.
/// </para>
/// <para>
/// <see cref="FakeProviderOptions"/> chooses the optional features in its discovery document. The token and PAR
/// endpoints check the client authentication strictly: exactly one method, and only a method that discovery lists.
/// </para>
/// </summary>
public sealed class FakeIdentityProvider : IAsyncDisposable
{
    public const string ClientId = "bff-client";
    public const string ClientSecret = "bff-secret";
    public static readonly string[] PromptValues = ["none", "login", "consent"];
    private static readonly string[] ResponseTypes = ["code"];
    private static readonly string[] SubjectTypes = ["public"];
    private static readonly string[] SigningAlgorithms = ["RS256"];
    private static readonly string[] DPoPAlgorithms = ["ES256"];

    private readonly WebApplication _app;
    private readonly FakeProviderOptions _options;
    // A key from parameters, not from one RSA instance: each signature provider then gets its own RSA instance,
    // so that parallel tests do not sign with one RSA instance at the same time, which can give a bad signature
    private volatile RsaSecurityKey _signingKey = new(CreateRsaParameters()) { KeyId = "test-key" };
    // A second key in the JWKS, with another algorithm than the RS256 of the client registration
    private readonly ECDsaSecurityKey _ecKey = new(ECDsa.Create(ECCurve.NamedCurves.nistP256)) { KeyId = "test-ec-key" };
    private readonly ConcurrentDictionary<string, Grant> _codes = new();
    private readonly ConcurrentDictionary<string, Grant> _accessTokens = new();
    private readonly ConcurrentDictionary<string, Grant> _refreshTokens = new();
    private readonly ConcurrentDictionary<string, bool> _usedRefreshTokens = new();
    private readonly ConcurrentDictionary<string, RequestGate> _refreshGates = new();
    private readonly ConcurrentQueue<IssuedTokens> _issued = new();
    private readonly ConcurrentDictionary<string, string> _revokeModes = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _pushedRequests = new();
    private readonly ConcurrentQueue<(string Token, string ClientAuthentication)> _revocations = new();
    private volatile RequestGate? _revocationGate;
    private volatile RequestGate? _jwksGate;

    private FakeIdentityProvider(WebApplication app, FakeProviderOptions options)
    {
        _app = app;
        _options = options;
    }

    public string Issuer { get; private set; } = "";

    /// <summary>
    /// Each token sent to the revocation endpoint.
    /// </summary>
    public ConcurrentBag<string> RevokedTokens { get; } = [];

    /// <summary>
    /// Each refresh token sent to the token endpoint with <c>grant_type=refresh_token</c>.
    /// </summary>
    public ConcurrentBag<string> RefreshRequests { get; } = [];

    /// <summary>
    /// The client authentication of each request to the token endpoint, with its <c>grant_type</c> and the
    /// <c>sub</c> of its grant (null when the code or token is unknown).
    /// </summary>
    public ConcurrentQueue<TokenRequest> TokenRequests { get; } = new();

    /// <summary>
    /// The client authentication of each request to the PAR endpoint, with the <c>request_uri</c> that it got
    /// (null when the client authentication failed).
    /// </summary>
    public ConcurrentQueue<PushedRequest> PushedAuthorizationRequests { get; } = new();

    /// <summary>
    /// The scheme of each request to the userinfo endpoint with a known token, with the <c>sub</c> of its grant and,
    /// for a DPoP-bound token, the check of its proof.
    /// </summary>
    public ConcurrentQueue<UserInfoRequest> UserInfoRequests { get; } = new();

    /// <summary>
    /// The parameters of each authorize request, the pushed ones included, for example <c>dpop_jkt</c>.
    /// </summary>
    public ConcurrentQueue<IReadOnlyDictionary<string, string>> AuthorizeRequests { get; } = new();

    private static RSAParameters CreateRsaParameters()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportParameters(includePrivateParameters: true);
    }

    public SigningCredentials SigningCredentials => new(_signingKey, SecurityAlgorithms.RsaSha256);

    /// <summary>
    /// ES256 with an EC key that the JWKS also lists.
    /// </summary>
    public SigningCredentials EcSigningCredentials => new(_ecKey, SecurityAlgorithms.EcdsaSha256);

    /// <summary>
    /// Replaces the signing key with a new key that has a new <c>kid</c>, as a key rollover does (Core 10.1.1).
    /// The JWKS then lists only the new key.
    /// </summary>
    public void RotateSigningKey() =>
        _signingKey = new RsaSecurityKey(CreateRsaParameters()) { KeyId = $"test-key-{Guid.NewGuid():N}" };

    public static async Task<FakeIdentityProvider> StartAsync(FakeProviderOptions? options = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var provider = new FakeIdentityProvider(app, options ?? new FakeProviderOptions());
        provider.MapEndpoints();

        await app.StartAsync();
        provider.Issuer = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>()
            .Addresses.Single().TrimEnd('/');
        return provider;
    }

    /// <summary>
    /// Signs a JWT with the key of the provider, or with <paramref name="credentials"/>. With
    /// <paramref name="withoutSignature"/>, the JWT has <c>alg=none</c> and no signature. With no
    /// <paramref name="tokenType"/>, the header has no <c>typ</c>.
    /// </summary>
    public string CreateJwt(Dictionary<string, object> claims, string? tokenType = null, SigningCredentials? credentials = null,
        bool withoutSignature = false)
    {
        var signing = withoutSignature ? null : credentials ?? SigningCredentials;
        var handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims,
            SigningCredentials = signing,
            TokenType = tokenType,
        });
        return tokenType is null && signing is not null ? WithoutTyp(token, signing) : token;
    }

    // JsonWebTokenHandler always writes typ=JWT when no type is given, so sign the payload again with a header without it
    private static string WithoutTyp(string token, SigningCredentials signing)
    {
        var header = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["alg"] = signing.Algorithm,
            ["kid"] = signing.Key.KeyId,
        }));
        var signingInput = $"{header}.{token.Split('.')[1]}";
        var factory = signing.CryptoProviderFactory ?? signing.Key.CryptoProviderFactory;
        var provider = factory.CreateForSigning(signing.Key, signing.Algorithm);
        try
        {
            var signature = provider.Sign(System.Text.Encoding.ASCII.GetBytes(signingInput));
            return $"{signingInput}.{Base64UrlEncoder.Encode(signature)}";
        }
        finally
        {
            factory.ReleaseSignatureProvider(provider);
        }
    }

    /// <summary>
    /// True when the revocation endpoint got a refresh token that the provider issued to <paramref name="subject"/>.
    /// </summary>
    public bool WasRefreshTokenRevoked(string subject) =>
        RevokedTokens.Any(t => _refreshTokens.TryGetValue(t, out var grant) && grant.Subject == subject);

    /// <summary>
    /// The client authentication of each accepted revocation of a refresh token that the provider issued to
    /// <paramref name="subject"/>, in order.
    /// </summary>
    public IReadOnlyList<string> RevocationClientAuthentications(string subject) =>
        _revocations.Where(r => _refreshTokens.TryGetValue(r.Token, out var grant) && grant.Subject == subject)
            .Select(r => r.ClientAuthentication).ToList();

    /// <summary>
    /// The number of refresh requests for the refresh tokens of <paramref name="subject"/>.
    /// </summary>
    public int RefreshCount(string subject) =>
        RefreshRequests.Count(t => _refreshTokens.TryGetValue(t, out var grant) && grant.Subject == subject);

    /// <summary>
    /// The refresh tokens that the provider issued to <paramref name="subject"/>, in order.
    /// </summary>
    public IReadOnlyList<string> IssuedRefreshTokens(string subject) =>
        _issued.Where(t => t.Subject == subject).Select(t => t.RefreshToken).ToList();

    /// <summary>
    /// The access tokens that the provider issued to <paramref name="subject"/>, in order.
    /// </summary>
    public IReadOnlyList<string> IssuedAccessTokens(string subject) =>
        _issued.Where(t => t.Subject == subject).Select(t => t.AccessToken).ToList();

    /// <summary>
    /// The ID tokens that the provider issued to <paramref name="subject"/>, in order.
    /// </summary>
    public IReadOnlyList<string> IssuedIdTokens(string subject) =>
        _issued.Where(t => t.Subject == subject && t.IdToken is not null).Select(t => t.IdToken!).ToList();

    /// <summary>
    /// Holds each refresh request of <paramref name="subject"/> until the test releases the gate. The gate tells
    /// the test when a refresh request arrives.
    /// </summary>
    public RequestGate HoldRefreshes(string subject) => _refreshGates.GetOrAdd(subject, _ => new RequestGate());

    /// <summary>
    /// Holds each request to the revocation endpoint until the test releases the gate. It holds the revocations of
    /// all users, so a test that uses it starts its own provider.
    /// </summary>
    public RequestGate HoldRevocations() => _revocationGate = new RequestGate();

    /// <summary>
    /// Holds each request to the JWKS endpoint until the test releases the gate. It holds the requests of all BFF
    /// instances, so a test that uses it starts its own provider.
    /// </summary>
    public RequestGate HoldJwks() => _jwksGate = new RequestGate();

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private void MapEndpoints()
    {
        _app.MapGet("/.well-known/openid-configuration", () =>
        {
            var discovery = new Dictionary<string, object>
            {
                ["issuer"] = _options.MismatchedDiscoveryIssuer ? $"{Issuer}INVALID" : Issuer,
                ["authorization_endpoint"] = $"{Issuer}/authorize",
                ["token_endpoint"] = $"{Issuer}/token",
                ["userinfo_endpoint"] = $"{Issuer}/userinfo",
                ["jwks_uri"] = $"{Issuer}/jwks",
                ["response_types_supported"] = ResponseTypes,
                ["subject_types_supported"] = SubjectTypes,
                ["id_token_signing_alg_values_supported"] = SigningAlgorithms,
                ["prompt_values_supported"] = PromptValues,
                ["token_endpoint_auth_methods_supported"] = _options.TokenEndpointAuthMethods,
            };
            if (_options.EndSession)
                discovery["end_session_endpoint"] = $"{Issuer}/endsession";
            if (_options.Revocation)
                discovery["revocation_endpoint"] = $"{Issuer}/revoke";
            if (_options.IssuerParameter)
                discovery["authorization_response_iss_parameter_supported"] = true;
            if (_options.BackChannelLogout)
                discovery["backchannel_logout_supported"] = true;
            if (_options.PushedAuthorization)
                discovery["pushed_authorization_request_endpoint"] = $"{Issuer}/par";
            if (_options.DPoP)
                discovery["dpop_signing_alg_values_supported"] = DPoPAlgorithms;
            return Results.Json(discovery);
        });

        _app.MapPost("/par", async (HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            var method = Authenticate(request, form);
            if (method is null)
            {
                PushedAuthorizationRequests.Enqueue(new PushedRequest(null, null));
                return Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var requestUri = $"urn:ietf:params:oauth:request_uri:{Guid.NewGuid():N}";
            PushedAuthorizationRequests.Enqueue(new PushedRequest(requestUri, method));
            _pushedRequests[requestUri] = form
                .Where(f => f.Key is not "client_secret")
                .ToDictionary(f => f.Key, f => f.Value.ToString());
            return Results.Json(new { request_uri = requestUri, expires_in = 60 }, statusCode: StatusCodes.Status201Created);
        });

        _app.MapGet("/jwks", async () =>
        {
            await WaitAtAsync(_jwksGate);
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(_signingKey);
            var ec = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(_ecKey);
            return Results.Json(new
            {
                keys = new object[]
                {
                    new { kty = jwk.Kty, use = "sig", kid = jwk.Kid, alg = "RS256", n = jwk.N, e = jwk.E },
                    new { kty = ec.Kty, use = "sig", kid = ec.Kid, crv = ec.Crv, x = ec.X, y = ec.Y },
                },
            });
        });

        _app.MapGet("/authorize", (HttpRequest request) =>
        {
            var query = request.Query;
            // A pushed request (RFC 9126): the parameters come from the PAR endpoint, the test parameters from the query
            var parameters = query.ToDictionary(q => q.Key, q => q.Value.ToString());
            if (query["request_uri"].FirstOrDefault() is { } requestUri)
            {
                if (!_pushedRequests.TryRemove(requestUri, out var pushed) || pushed["client_id"] != query["client_id"])
                    return Results.BadRequest(new { error = "invalid_request_uri" });
                foreach (var (key, value) in pushed)
                    parameters[key] = value;
            }

            AuthorizeRequests.Enqueue(parameters);
            string? Parameter(string name) => parameters.TryGetValue(name, out var value) ? value : null;
            var redirectUri = Parameter("redirect_uri") ?? "";
            var state = Uri.EscapeDataString(Parameter("state") ?? "");
            var iss = Parameter("test_iss") switch
            {
                "omit" => "",
                { } value => $"&iss={Uri.EscapeDataString(value)}",
                null when _options.IssuerParameter => $"&iss={Uri.EscapeDataString(Issuer)}",
                null => "",
            };

            if (Parameter("test_error") is { } error)
                return Results.Redirect($"{redirectUri}?error={Uri.EscapeDataString(error)}&state={state}{iss}");

            var grant = new Grant(
                Subject: Parameter("test_sub") ?? "user-1",
                SessionId: Parameter("test_sid"),
                Nonce: Parameter("test_nonce") == "wrong" ? "wrong-nonce" : Parameter("nonce") ?? "",
                ExpiresIn: int.TryParse(Parameter("test_expires_in"), out var expiresIn) ? expiresIn : 3600,
                RefreshExpiresIn: int.TryParse(Parameter("test_refresh_expires_in"), out var refreshExpiresIn) ? refreshExpiresIn : null,
                Refresh: Parameter("test_refresh"),
                ExtraAudience: Parameter("test_aud"),
                AuthorizedParty: Parameter("test_azp"),
                WrongAtHash: Parameter("test_at_hash") == "wrong",
                UnsignedIdToken: Parameter("test_id_token") == "unsigned",
                OmitExpiresIn: Parameter("test_expires_in") == "omit",
                RefreshIdToken: Parameter("test_refresh_id_token"),
                AuthTime: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                RedirectUri: redirectUri,
                CodeChallenge: Parameter("code_challenge_method") == "S256" ? Parameter("code_challenge") : null,
                DPoPJkt: _options.DPoP ? Parameter("dpop_jkt") : null);

            if (Parameter("test_revoke") is { } revokeMode)
                _revokeModes[grant.Subject] = revokeMode;

            var code = NewToken("code");
            _codes[code] = grant;

            var location = $"{redirectUri}?code={code}&state={state}{iss}";
            if (Parameter("test_session_state") is { } sessionState)
                location += $"&session_state={Uri.EscapeDataString(sessionState)}";

            return Results.Redirect(location);
        });

        _app.MapPost("/token", async (HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            var grantType = form["grant_type"].ToString();
            var method = Authenticate(request, form);
            _codes.TryGetValue(form["code"].ToString(), out var codeGrant);
            _refreshTokens.TryGetValue(form["refresh_token"].ToString(), out var refreshGrant);
            var proofHeader = request.Headers["DPoP"].ToString();
            var proof = _options.DPoP && proofHeader.Length > 0
                ? DPoPProofVerifier.Verify(proofHeader, "POST", $"{Issuer}/token")
                : null;
            TokenRequests.Enqueue(new TokenRequest(grantType, method, (codeGrant ?? refreshGrant)?.Subject, proof));
            if (method is null)
                return Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized);

            // RFC 9449 section 5: a grant that the login bound to a key needs a valid proof of that key
            if (_options.DPoP && (codeGrant ?? refreshGrant) is { DPoPJkt: { } boundJkt } && (proof is not { Valid: true } || proof.Jkt != boundJkt))
                return Results.Json(new { error = "invalid_dpop_proof" }, statusCode: StatusCodes.Status400BadRequest);

            switch (grantType)
            {
                case "authorization_code" when _codes.TryRemove(form["code"].ToString(), out var grant):
                    // RFC 6749 section 4.1.3 and RFC 7636 section 4.6: the code is bound to its redirect_uri and its S256 challenge
                    if (form["redirect_uri"].ToString() != grant.RedirectUri
                        || grant.CodeChallenge is null
                        || CodeChallenge(form["code_verifier"].ToString()) != grant.CodeChallenge)
                        return Results.Json(new { error = "invalid_grant" }, statusCode: StatusCodes.Status400BadRequest);

                    return IssueTokens(grant, refresh: false);

                case "refresh_token" when _refreshTokens.TryGetValue(form["refresh_token"].ToString(), out var grant):
                    RefreshRequests.Add(form["refresh_token"].ToString());
                    if (_refreshGates.TryGetValue(grant.Subject, out var gate))
                    {
                        gate.Arrived.TrySetResult();
                        await gate.Released.Task;
                    }

                    if (grant.Refresh == "unavailable")
                        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

                    var firstUse = _usedRefreshTokens.TryAdd(form["refresh_token"].ToString(), true);
                    return grant.Refresh == "fail" || !firstUse
                        ? Results.Json(new { error = "invalid_grant" }, statusCode: StatusCodes.Status400BadRequest)
                        : IssueTokens(grant, refresh: true);

                default:
                    return Results.Json(new { error = "invalid_grant" }, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        _app.MapMethods("/userinfo", ["GET", "POST"], (HttpRequest request) =>
        {
            if (!AuthenticationHeaderValue.TryParse(request.Headers.Authorization.ToString(), out var header)
                || header.Parameter is not { } accessToken
                || !_accessTokens.TryGetValue(accessToken, out var grant))
            {
                return Results.Unauthorized();
            }

            // RFC 9449 section 7.1: a DPoP-bound token needs the DPoP scheme and a proof of its key for this
            // request and this token
            if (_options.UserInfoBearerOnly)
            {
                UserInfoRequests.Enqueue(new UserInfoRequest(header.Scheme, grant.Subject, null));
                if (!string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
                {
                    request.HttpContext.Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
                    return Results.StatusCode(StatusCodes.Status401Unauthorized);
                }
            }
            else if (grant.DPoPJkt is { } boundJkt)
            {
                var proof = DPoPProofVerifier.Verify(request.Headers["DPoP"].ToString(), request.Method, $"{Issuer}/userinfo", accessToken);
                UserInfoRequests.Enqueue(new UserInfoRequest(header.Scheme, grant.Subject, proof));
                if (!string.Equals(header.Scheme, "DPoP", StringComparison.OrdinalIgnoreCase) || proof is not { Valid: true } || proof.Jkt != boundJkt)
                    return DPoPError(request, "invalid_dpop_proof");
                if (_options.UserInfoNonce is { } nonce && proof.Nonce != nonce)
                {
                    request.HttpContext.Response.Headers["DPoP-Nonce"] = nonce;
                    return DPoPError(request, "use_dpop_nonce");
                }
            }
            else
            {
                UserInfoRequests.Enqueue(new UserInfoRequest(header.Scheme, grant.Subject, null));
                if (!string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
                    return Results.Unauthorized();
            }

            return Results.Json(new { sub = grant.Subject, name = $"Name of {grant.Subject}", email = $"{grant.Subject}@example.com" });
        });

        _app.MapPost("/revoke", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            var method = Authenticate(context.Request, form);
            if (method is null)
                return Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized);

            await WaitAtAsync(_revocationGate);
            var token = form["token"].ToString();
            RevokedTokens.Add(token);
            _revocations.Enqueue((token, method));

            var mode = _refreshTokens.TryGetValue(token, out var grant) ? _revokeModes.GetValueOrDefault(grant.Subject) : null;
            switch (mode)
            {
                case "fail":
                    return Results.StatusCode(StatusCodes.Status500InternalServerError);
                case "drop":
                    context.Abort();
                    return Results.Empty;
                default:
                    return Results.Ok();
            }
        });
    }

    private static async Task WaitAtAsync(RequestGate? gate)
    {
        if (gate is null)
            return;

        gate.Arrived.TrySetResult();
        await gate.Released.Task;
    }

    private static IResult DPoPError(HttpRequest request, string error)
    {
        request.HttpContext.Response.Headers.WWWAuthenticate = $"DPoP error=\"{error}\", algs=\"ES256\"";
        return Results.StatusCode(StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// The client authentication method of a request, <c>client_secret_basic</c> or <c>client_secret_post</c>, or
    /// null when it fails: a wrong secret, more than one method (RFC 6749 section 2.3), or a method that discovery
    /// does not list.
    /// </summary>
    private string? Authenticate(HttpRequest request, IFormCollection form)
    {
        var hasBasic = AuthenticationHeaderValue.TryParse(request.Headers.Authorization.ToString(), out var header)
            && string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase);
        var hasPost = form.ContainsKey("client_secret");

        string method;
        string? clientId;
        string? clientSecret;
        if (hasBasic && !hasPost)
        {
            // RFC 6749 section 2.3.1: each part is form-encoded before the Base64 step
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header!.Parameter ?? "")).Split(':', 2);
            method = "client_secret_basic";
            clientId = WebUtility.UrlDecode(decoded[0]);
            clientSecret = decoded.Length == 2 ? WebUtility.UrlDecode(decoded[1]) : null;
            if (form.TryGetValue("client_id", out var bodyClientId) && bodyClientId != clientId)
                return null;
        }
        else if (hasPost && !hasBasic)
        {
            method = "client_secret_post";
            clientId = form["client_id"];
            clientSecret = form["client_secret"];
        }
        else
        {
            return null;
        }

        return _options.TokenEndpointAuthMethods.Contains(method) && clientId == ClientId && clientSecret == ClientSecret
            ? method
            : null;
    }

    private IResult IssueTokens(Grant grant, bool refresh)
    {
        var accessToken = NewToken("at");
        var refreshToken = NewToken("rt");
        _accessTokens[accessToken] = grant;
        _refreshTokens[refreshToken] = grant;

        string? idToken = null;
        var response = new Dictionary<string, object>
        {
            ["access_token"] = accessToken,
            ["token_type"] = grant.DPoPJkt is null ? "Bearer" : "DPoP",
            ["refresh_token"] = refreshToken,
        };
        if (!grant.OmitExpiresIn)
            response["expires_in"] = refresh ? grant.RefreshExpiresIn ?? grant.ExpiresIn : grant.ExpiresIn;

        if (!refresh)
        {
            var claims = IdTokenClaims(grant);
            claims["nonce"] = grant.Nonce;
            claims["at_hash"] = AtHash(grant.WrongAtHash ? NewToken("at") : accessToken);
            if (grant.ExtraAudience is { } extra)
                claims["aud"] = new[] { ClientId, extra };

            idToken = CreateJwt(claims, "JWT", withoutSignature: grant.UnsignedIdToken);
            response["id_token"] = idToken;
        }
        else if (grant.RefreshIdToken is { } fault)
        {
            idToken = RefreshIdToken(grant, fault);
            response["id_token"] = idToken;
        }

        _issued.Enqueue(new IssuedTokens(grant.Subject, accessToken, refreshToken, idToken));

        return Results.Text(JsonSerializer.Serialize(response), "application/json");
    }

    private Dictionary<string, object> IdTokenClaims(Grant grant)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new Dictionary<string, object>
        {
            ["iss"] = Issuer,
            ["aud"] = ClientId,
            ["sub"] = grant.Subject,
            ["iat"] = now,
            ["exp"] = now + 300,
            ["auth_time"] = grant.AuthTime,
        };
        if (grant.SessionId is not null)
            claims["sid"] = grant.SessionId;
        if (grant.AuthorizedParty is not null)
            claims["azp"] = grant.AuthorizedParty;
        return claims;
    }

    /// <summary>
    /// The ID token of a refresh response (OIDC Core 12.2), with no nonce, and with the fault that
    /// <c>test_refresh_id_token</c> names.
    /// </summary>
    private string RefreshIdToken(Grant grant, string fault)
    {
        var claims = IdTokenClaims(grant);
        switch (fault)
        {
            case "valid" or "bad_signature":
                break;
            case "wrong_iss":
                claims["iss"] = $"{Issuer}/other";
                break;
            case "wrong_sub":
                claims["sub"] = $"other-{grant.Subject}";
                break;
            case "wrong_aud":
                claims["aud"] = "other-client";
                break;
            case "wrong_azp":
                claims["azp"] = "other-client";
                break;
            case "wrong_auth_time":
                claims["auth_time"] = grant.AuthTime - 60;
                break;
            case "expired":
                // Past the default clock skew of 5 minutes
                claims["iat"] = (long)claims["iat"] - 900;
                claims["exp"] = (long)claims["iat"] + 300;
                break;
            default:
                throw new InvalidOperationException($"Unknown test_refresh_id_token value '{fault}'.");
        }

        var credentials = fault == "bad_signature"
            ? new SigningCredentials(new RsaSecurityKey(CreateRsaParameters()) { KeyId = _signingKey.KeyId }, SecurityAlgorithms.RsaSha256)
            : null;
        return CreateJwt(claims, "JWT", credentials);
    }

    private static string NewToken(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    // Core 3.1.3.6: the left half of the SHA-256 hash of the access token, base64url-encoded
    private static string AtHash(string accessToken) =>
        Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(accessToken))[..16]);

    // RFC 7636 section 4.2: S256 is the base64url-encoded SHA-256 hash of the ASCII code_verifier
    private static string CodeChallenge(string codeVerifier) =>
        Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    /// <param name="Refresh">The <c>test_refresh</c> mode: null, <c>fail</c>, or <c>unavailable</c>.</param>
    /// <param name="RefreshIdToken">The <c>test_refresh_id_token</c> mode, or null for no ID token in a refresh response.</param>
    /// <param name="AuthTime">The <c>auth_time</c> of the ID tokens.</param>
    /// <param name="DPoPJkt">The <c>dpop_jkt</c> of the authorize request. The tokens of the grant are bound to it.</param>
    private sealed record Grant(string Subject, string? SessionId, string Nonce, int ExpiresIn, int? RefreshExpiresIn, string? Refresh,
        string? ExtraAudience, string? AuthorizedParty, bool WrongAtHash, bool UnsignedIdToken, bool OmitExpiresIn,
        string? RefreshIdToken, long AuthTime, string RedirectUri, string? CodeChallenge, string? DPoPJkt = null);

    /// <summary>
    /// One request to the token endpoint.
    /// </summary>
    /// <param name="GrantType">The <c>grant_type</c>.</param>
    /// <param name="ClientAuthentication"><c>client_secret_basic</c>, <c>client_secret_post</c>, or null when the
    /// client authentication failed.</param>
    /// <param name="Subject">The <c>sub</c> of the code or refresh token, or null when it is unknown.</param>
    /// <param name="DPoP">The <c>DPoP</c> proof of the request: null when it has none.</param>
    public sealed record TokenRequest(string GrantType, string? ClientAuthentication, string? Subject, DPoPProofResult? DPoP = null);

    /// <param name="Scheme">The scheme of the <c>Authorization</c> header.</param>
    /// <param name="DPoP">The check of the <c>DPoP</c> proof: null when the token is not DPoP-bound.</param>
    public sealed record UserInfoRequest(string Scheme, string Subject, DPoPProofResult? DPoP);

    /// <summary>
    /// One request to the PAR endpoint.
    /// </summary>
    /// <param name="RequestUri">The <c>request_uri</c> of the pushed request, or null when the client
    /// authentication failed.</param>
    /// <param name="ClientAuthentication"><c>client_secret_basic</c>, <c>client_secret_post</c>, or null when the
    /// client authentication failed.</param>
    public sealed record PushedRequest(string? RequestUri, string? ClientAuthentication);

    private sealed record IssuedTokens(string Subject, string AccessToken, string RefreshToken, string? IdToken);

    /// <summary>
    /// See <see cref="HoldRefreshes"/> and <see cref="HoldingCommandInterceptor"/>.
    /// </summary>
    public sealed class RequestGate
    {
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
