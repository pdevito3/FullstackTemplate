namespace FullstackTemplate.Server.Domain.DPoPProofs;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// One row of the replay cache of the DPoP proofs in <c>appdb</c> (RFC 9449 section 11.1). A second proof with the
/// same key is a replay. It is a table of the authentication check, not an aggregate: it has no events and no tenant.
/// </summary>
/// <param name="Id">The SHA-256 of the key thumbprint and the <c>jti</c> of the proof, in base64url. A proof
/// is unique for the key that signed it.</param>
/// <param name="Expires">The time after which the proof cannot pass the <c>iat</c> check. The cleanup then
/// deletes the row.</param>
public sealed record UsedDPoPProof(string Id, DateTimeOffset Expires)
{
    public static string KeyFor(string jwkThumbprint, string jti) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{jwkThumbprint}\n{jti}")))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
