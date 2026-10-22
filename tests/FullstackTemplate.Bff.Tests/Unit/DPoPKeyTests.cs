namespace FullstackTemplate.Bff.Tests.Unit;

using System.Security.Cryptography;
using System.Text.Json;
using FullstackTemplate.Bff.DPoP;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

/// <summary>
/// The startup check of <c>Auth:DPoPJsonWebKey</c>: a key that cannot make a valid proof stops the BFF, so that
/// it does not fail each login at runtime.
/// </summary>
public class DPoPKeyTests
{
    [Test]
    public void an_es256_key_on_p256_passes()
    {
        Read(DPoPTestKey.Create().Jwk).ShouldNotBeNull();
    }

    [Test]
    [Arguments("ES384", "P-384")]
    [Arguments("ES512", "P-521")]
    public void an_ec_key_with_the_alg_of_its_curve_passes(string alg, string crv)
    {
        Read(EcKey(alg, crv)).ShouldNotBeNull();
    }

    [Test]
    public void an_rsa_key_with_ps256_passes()
    {
        Read(RsaKey("PS256")).ShouldNotBeNull();
    }

    [Test]
    public void an_ec_key_with_an_rsa_alg_fails()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Read(EcKey("RS256", "P-256")));

        exception.Message.ShouldContain("needs a RSA key");
    }

    [Test]
    public void es256_with_a_p384_key_fails()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Read(EcKey("ES256", "P-384")));

        exception.Message.ShouldContain("curve P-256");
        exception.Message.ShouldContain("crv P-384");
    }

    [Test]
    public void an_rsa_key_with_an_ec_alg_fails()
    {
        Should.Throw<InvalidOperationException>(() => Read(RsaKey("ES256"))).Message.ShouldContain("needs a EC key");
    }

    [Test]
    [Arguments("HS256")]
    [Arguments("none")]
    [Arguments("EdDSA")]
    public void an_alg_that_is_not_an_asymmetric_proof_alg_fails(string alg)
    {
        Should.Throw<InvalidOperationException>(() => Read(EcKey(alg, "P-256"))).Message.ShouldContain("not an asymmetric algorithm");
    }

    [Test]
    public void a_private_part_of_another_key_fails()
    {
        var key = Members(DPoPTestKey.Create().Jwk);
        key["d"] = Members(DPoPTestKey.Create().Jwk)["d"];

        var exception = Should.Throw<InvalidOperationException>(() => Read(JsonSerializer.Serialize(key)));

        // Linux fails the signature and macOS fails the verify, with the same message
        exception.Message.ShouldBe(
            "Auth:DPoPJsonWebKey has a private part that does not match its public part, so a test signature with alg ES256 fails.");
        exception.InnerException.ShouldBeNull();
    }

    private static Duende.AccessTokenManagement.DPoP.DPoPProofKey? Read(string jwk) =>
        DPoPKey.Read(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:Features:DPoP"] = "Require",
            [DPoPKey.ConfigurationKey] = jwk,
        }).Build());

    private static Dictionary<string, string> Members(string jwk) => JsonSerializer.Deserialize<Dictionary<string, string>>(jwk)!;

    private static string EcKey(string alg, string crv)
    {
        var curve = crv switch
        {
            "P-384" => ECCurve.NamedCurves.nistP384,
            "P-521" => ECCurve.NamedCurves.nistP521,
            _ => ECCurve.NamedCurves.nistP256,
        };
        using var ecdsa = ECDsa.Create(curve);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: true);
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = crv,
            ["alg"] = alg,
            ["x"] = Base64UrlEncoder.Encode(parameters.Q.X!),
            ["y"] = Base64UrlEncoder.Encode(parameters.Q.Y!),
            ["d"] = Base64UrlEncoder.Encode(parameters.D!),
        });
    }

    private static string RsaKey(string alg)
    {
        using var rsa = RSA.Create(2048);
        var p = rsa.ExportParameters(includePrivateParameters: true);
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "RSA",
            ["alg"] = alg,
            ["n"] = Base64UrlEncoder.Encode(p.Modulus!),
            ["e"] = Base64UrlEncoder.Encode(p.Exponent!),
            ["d"] = Base64UrlEncoder.Encode(p.D!),
            ["p"] = Base64UrlEncoder.Encode(p.P!),
            ["q"] = Base64UrlEncoder.Encode(p.Q!),
            ["dp"] = Base64UrlEncoder.Encode(p.DP!),
            ["dq"] = Base64UrlEncoder.Encode(p.DQ!),
            ["qi"] = Base64UrlEncoder.Encode(p.InverseQ!),
        });
    }
}
