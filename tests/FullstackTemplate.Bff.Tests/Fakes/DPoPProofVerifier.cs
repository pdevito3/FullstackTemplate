namespace FullstackTemplate.Bff.Tests.Fakes;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// The result of <see cref="DPoPProofVerifier.Verify"/>. <paramref name="Error"/> is null for a valid proof.
/// </summary>
public sealed record DPoPProofResult(string? Error, string? Jkt, JsonElement Payload)
{
    public bool Valid => Error is null;

    /// <summary>
    /// The <c>nonce</c> claim, or null.
    /// </summary>
    public string? Nonce => Payload.ValueKind == JsonValueKind.Object && Payload.TryGetProperty("nonce", out var nonce) ? nonce.GetString() : null;
}

/// <summary>
/// The checks of RFC 9449 section 4.3 that the fake provider and the tests need to see that the BFF sends a correct
/// ES256 proof: <c>typ</c>, the signature with the <c>jwk</c> of the header, <c>htm</c>, <c>htu</c>, <c>iat</c>,
/// <c>jti</c>, and <c>ath</c> when a token is given.
/// </summary>
public static class DPoPProofVerifier
{
    public static DPoPProofResult Verify(string? proof, string method, string url, string? accessToken = null)
    {
        var none = default(JsonElement);
        var parts = proof?.Split('.');
        if (parts is not { Length: 3 })
            return new DPoPProofResult("not a JWT", null, none);

        try
        {
            using var header = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(parts[0]));
            var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(parts[1])).RootElement.Clone();
            string? Fail(string error) => error;

            if (header.RootElement.GetProperty("typ").GetString() != "dpop+jwt")
                return new DPoPProofResult(Fail("typ"), null, payload);
            if (header.RootElement.GetProperty("alg").GetString() != "ES256")
                return new DPoPProofResult(Fail("alg"), null, payload);

            var jwkElement = header.RootElement.GetProperty("jwk");
            if (jwkElement.TryGetProperty("d", out _))
                return new DPoPProofResult(Fail("jwk has a private key"), null, payload);

            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = Base64UrlEncoder.DecodeBytes(jwkElement.GetProperty("x").GetString()),
                    Y = Base64UrlEncoder.DecodeBytes(jwkElement.GetProperty("y").GetString()),
                },
            });
            var signatureValid = ecdsa.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64UrlEncoder.DecodeBytes(parts[2]),
                HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            var jkt = Base64UrlEncoder.Encode(new JsonWebKey(jwkElement.GetRawText()).ComputeJwkThumbprint());
            if (!signatureValid)
                return new DPoPProofResult("signature", jkt, payload);
            if (payload.GetProperty("htm").GetString() != method)
                return new DPoPProofResult("htm", jkt, payload);
            if (payload.GetProperty("htu").GetString() != url)
                return new DPoPProofResult("htu", jkt, payload);
            if (string.IsNullOrEmpty(payload.GetProperty("jti").GetString()))
                return new DPoPProofResult("jti", jkt, payload);
            if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - payload.GetProperty("iat").GetInt64()) > 60)
                return new DPoPProofResult("iat", jkt, payload);
            if (accessToken is not null
                && (!payload.TryGetProperty("ath", out var ath) || ath.GetString() != Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(accessToken)))))
                return new DPoPProofResult("ath", jkt, payload);

            return new DPoPProofResult(null, jkt, payload);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or CryptographicException)
        {
            return new DPoPProofResult($"malformed: {ex.GetType().Name}", null, none);
        }
    }
}

/// <summary>
/// A new EC P-256 key as a private JWK with <c>alg=ES256</c>, as <c>Auth:DPoPJsonWebKey</c> holds it.
/// </summary>
public sealed record DPoPTestKey(string Jwk, string Jkt)
{
    public static DPoPTestKey Create()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: true);
        var jwk = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["alg"] = "ES256",
            ["use"] = "sig",
            ["x"] = Base64UrlEncoder.Encode(parameters.Q.X!),
            ["y"] = Base64UrlEncoder.Encode(parameters.Q.Y!),
            ["d"] = Base64UrlEncoder.Encode(parameters.D!),
        });
        return new DPoPTestKey(jwk, Base64UrlEncoder.Encode(new JsonWebKey(jwk).ComputeJwkThumbprint()));
    }
}
