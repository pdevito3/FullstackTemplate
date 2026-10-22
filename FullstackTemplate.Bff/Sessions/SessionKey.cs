using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace FullstackTemplate.Bff.Sessions;

/// <summary>
/// The key of a BFF session: 256 random bits in base64url. The key is a credential, so a log shows only its
/// <see cref="Fingerprint"/>.
/// </summary>
public static class SessionKey
{
    /// <summary>
    /// The claim type in which the cookie handler stores the key in the cookie ticket.
    /// <c>CookieAuthenticationHandler</c> keeps this name private.
    /// </summary>
    public const string ClaimType = "Microsoft.AspNetCore.Authentication.Cookies-SessionId";

    public static string Create() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
}
