using System.Security.Cryptography;
using System.Text;

namespace FullstackTemplate.Bff;

/// <summary>
/// A short form of a secret, such as a BFF session key or a token, for logs. Never log the secret itself.
/// </summary>
public static class Fingerprint
{
    /// <summary>
    /// The first 8 hex characters of the SHA-256 of <paramref name="secret"/>. Enough to match log lines, not
    /// enough to use the secret.
    /// </summary>
    public static string Of(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)))[..8];
}
