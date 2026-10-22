using Duende.AccessTokenManagement.DPoP;
using FullstackTemplate.Bff.Features;
using Microsoft.IdentityModel.Tokens;

namespace FullstackTemplate.Bff.DPoP;

/// <summary>
/// The DPoP key of the BFF (RFC 9449): one private JSON Web Key from <c>Auth:DPoPJsonWebKey</c>. The BFF uses it
/// for every user session, so that the key of a session is the same for the authorize request, the code
/// exchange, the refresh, and the API calls. Token management stores the key in the BFF session with the tokens.
/// </summary>
public static class DPoPKey
{
    public const string ConfigurationKey = "Auth:DPoPJsonWebKey";

    // The asymmetric algorithms of a proof (RFC 9449 section 4.2), with the key type and curve that each one needs
    // (RFC 7518 section 3.1). Token management makes proofs with EC and RSA keys only.
    private static readonly Dictionary<string, (string KeyType, string? Curve)> Algorithms = new(StringComparer.Ordinal)
    {
        [SecurityAlgorithms.EcdsaSha256] = (JsonWebAlgorithmsKeyTypes.EllipticCurve, JsonWebKeyECTypes.P256),
        [SecurityAlgorithms.EcdsaSha384] = (JsonWebAlgorithmsKeyTypes.EllipticCurve, JsonWebKeyECTypes.P384),
        [SecurityAlgorithms.EcdsaSha512] = (JsonWebAlgorithmsKeyTypes.EllipticCurve, JsonWebKeyECTypes.P521),
        [SecurityAlgorithms.RsaSha256] = (JsonWebAlgorithmsKeyTypes.RSA, null),
        [SecurityAlgorithms.RsaSha384] = (JsonWebAlgorithmsKeyTypes.RSA, null),
        [SecurityAlgorithms.RsaSha512] = (JsonWebAlgorithmsKeyTypes.RSA, null),
        [SecurityAlgorithms.RsaSsaPssSha256] = (JsonWebAlgorithmsKeyTypes.RSA, null),
        [SecurityAlgorithms.RsaSsaPssSha384] = (JsonWebAlgorithmsKeyTypes.RSA, null),
        [SecurityAlgorithms.RsaSsaPssSha512] = (JsonWebAlgorithmsKeyTypes.RSA, null),
    };

    /// <summary>
    /// The key, or null when <c>Auth:Features:DPoP</c> is <c>Disable</c> or the setting is empty. Throws when the
    /// key is not a private key with an <c>alg</c>, when the <c>alg</c> is not asymmetric or does not fit the key
    /// type and curve, or when a test signature fails. The message never has the key.
    /// </summary>
    public static DPoPProofKey? Read(IConfiguration configuration)
    {
        var mode = configuration.GetValue("Auth:Features:DPoP", FeatureMode.Disable);
        var value = configuration[ConfigurationKey];
        if (mode == FeatureMode.Disable || string.IsNullOrWhiteSpace(value))
            return null;

        JsonWebKey jwk;
        try
        {
            jwk = new JsonWebKey(value);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{ConfigurationKey} is not a JSON Web Key.", ex);
        }

        if (!jwk.HasPrivateKey)
            throw new InvalidOperationException($"{ConfigurationKey} must be a private key.");
        if (string.IsNullOrEmpty(jwk.Alg))
            throw new InvalidOperationException($"{ConfigurationKey} must have an alg, for example ES256.");
        if (!Algorithms.TryGetValue(jwk.Alg, out var fit))
            throw new InvalidOperationException(
                $"{ConfigurationKey} has alg {jwk.Alg}, which is not an asymmetric algorithm for a DPoP proof. Use one of {string.Join(", ", Algorithms.Keys)}.");
        if (jwk.Kty != fit.KeyType || (fit.Curve is not null && jwk.Crv != fit.Curve))
            throw new InvalidOperationException(
                $"{ConfigurationKey} has alg {jwk.Alg}, which needs a {fit.KeyType} key{(fit.Curve is null ? "" : $" on curve {fit.Curve}")}, but the key has kty {jwk.Kty}{(string.IsNullOrEmpty(jwk.Crv) ? "" : $" and crv {jwk.Crv}")}.");

        CheckTestSignature(jwk);
        return DPoPProofKey.Parse(value);
    }

    /// <summary>
    /// Signs a test value with the private key and verifies it with the public members alone, as a provider does
    /// with the <c>jwk</c> of a proof. It fails for a private part that does not match the public part.
    /// </summary>
    /// <remarks>
    /// The platforms fail a mismatched key at different steps: OpenSSL on Linux throws when it signs, and macOS
    /// signs and then fails the verify. Both give the same error.
    /// </remarks>
    private static void CheckTestSignature(JsonWebKey jwk)
    {
        var publicKey = jwk.Kty == JsonWebAlgorithmsKeyTypes.RSA
            ? new JsonWebKey { Kty = jwk.Kty, N = jwk.N, E = jwk.E }
            : new JsonWebKey { Kty = jwk.Kty, Crv = jwk.Crv, X = jwk.X, Y = jwk.Y };
        var data = "dpop key check"u8.ToArray();

        bool verified;
        try
        {
            var signer = CryptoProviderFactory.Default.CreateForSigning(jwk, jwk.Alg);
            var verifier = CryptoProviderFactory.Default.CreateForVerifying(publicKey, jwk.Alg);
            try
            {
                verified = verifier.Verify(data, signer.Sign(data));
            }
            finally
            {
                CryptoProviderFactory.Default.ReleaseSignatureProvider(signer);
                CryptoProviderFactory.Default.ReleaseSignatureProvider(verifier);
            }
        }
        // Do not keep the exception: its message can have parts of the key
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            verified = false;
        }

        if (!verified)
            throw new InvalidOperationException(
                $"{ConfigurationKey} has a private part that does not match its public part, so a test signature with alg {jwk.Alg} fails.");
    }

    /// <summary>
    /// <see cref="Read"/>, but null for a key that is not valid. The options of token management use it: Duende
    /// resolves them while the host builds its services, and an exception there races with the dispose of the host.
    /// <see cref="ProviderFeatureDiscovery"/> calls <see cref="Read"/> at startup, so that a key that is not valid
    /// still stops the BFF.
    /// </summary>
    public static DPoPProofKey? ReadOrNull(IConfiguration configuration)
    {
        try
        {
            return Read(configuration);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// The <c>alg</c> of the key.
    /// </summary>
    public static string Algorithm(DPoPProofKey key) => new JsonWebKey(key.ToString()).Alg;
}
