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

    /// <summary>
    /// The key, or null when <c>Auth:Features:DPoP</c> is <c>Disable</c> or the setting is empty. Throws when the
    /// key is not a private key with an <c>alg</c>. The message never has the key.
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

        return DPoPProofKey.Parse(value);
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
