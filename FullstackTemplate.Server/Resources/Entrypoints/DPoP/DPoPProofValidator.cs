namespace FullstackTemplate.Server.Resources.Entrypoints.DPoP;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// What a DPoP proof must match: the request that carries it and the access token that it goes with.
/// </summary>
/// <param name="Method">The HTTP method of the request.</param>
/// <param name="Url">The URL of the request. The check ignores the query and the fragment.</param>
/// <param name="AccessToken">The access token as the client sent it.</param>
/// <param name="AccessTokenJkt">The <c>cnf.jkt</c> of the access token.</param>
public sealed record DPoPRequest(string Method, Uri Url, string AccessToken, string AccessTokenJkt);

/// <summary>
/// The public key of a DPoP proof, built from the members of its thumbprint only, and the thumbprint (RFC 7638).
/// </summary>
public sealed record DPoPPublicKey(JsonWebKey Key, string Jkt);

/// <summary>
/// The result of a proof check. <see cref="Error"/> is the <c>error</c> value of the <c>WWW-Authenticate</c> header
/// (RFC 9449 section 7.1).
/// </summary>
public sealed record DPoPValidationResult(string? Error, string? Description, string? Jkt)
{
    public const string InvalidProof = "invalid_dpop_proof";
    public const string InvalidToken = "invalid_token";

    public bool Succeeded => Error is null;

    public static DPoPValidationResult Success(string jkt) => new(null, null, jkt);
    public static DPoPValidationResult Proof(string description) => new(InvalidProof, description, null);
    public static DPoPValidationResult Token(string description) => new(InvalidToken, description, null);
}

/// <summary>
/// Checks a DPoP proof with the rules of RFC 9449 section 4.3, and the key binding of the access token (section 7.1).
/// The check for one DPoP header field (rule 1) is in <see cref="DPoPAuthentication"/>, which reads the headers.
/// </summary>
public sealed class DPoPProofValidator(IOptions<DPoPOptions> options, TimeProvider timeProvider, IDPoPReplayCache replayCache)
{
    public const string ProofType = "dpop+jwt";

    /// <summary>
    /// The registered asymmetric algorithms that the server accepts. The <c>algs</c> parameter of the challenge
    /// lists them.
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedAlgorithms =
    [
        SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.EcdsaSha384, SecurityAlgorithms.EcdsaSha512,
        SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.RsaSsaPssSha384, SecurityAlgorithms.RsaSsaPssSha512,
        SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512,
    ];

    // The members of a JWK that hold private key material (RFC 7518 sections 6.2.2, 6.3.2, and 6.4)
    private static readonly string[] PrivateKeyMembers = ["d", "p", "q", "dp", "dq", "qi", "oth", "k"];

    // The members of a JWK that point to a certificate (RFC 7517 section 4). IdentityModel can verify
    // with the certificate of x5c in place of n and e, so a proof must carry only the key itself. A kid is ignored,
    // because the key that verifies the proof has only the thumbprint members.
    private static readonly string[] KeyReferenceMembers = ["x5c", "x5u", "x5t", "x5t#S256"];

    public async Task<DPoPValidationResult> ValidateAsync(string proof, DPoPRequest request, CancellationToken cancellationToken)
    {
        // Rules 2 and 3: a well-formed JWT
        JsonWebToken jwt;
        try
        {
            jwt = new JsonWebToken(proof);
        }
        catch (Exception ex) when (ex is ArgumentException or SecurityTokenMalformedException)
        {
            return DPoPValidationResult.Proof("The DPoP proof is not a well-formed JWT.");
        }

        // Rule 4
        if (!string.Equals(jwt.Typ, ProofType, StringComparison.Ordinal))
            return DPoPValidationResult.Proof($"The typ header must be {ProofType}.");

        // Rule 5
        if (string.IsNullOrEmpty(jwt.Alg) || !AllowedAlgorithms.Contains(jwt.Alg, StringComparer.Ordinal))
            return DPoPValidationResult.Proof("The alg header must be an asymmetric signature algorithm that the server accepts.");

        // Rules 6 and 7: the key
        if (!jwt.TryGetHeaderValue<JsonElement>("jwk", out var jwkElement) || jwkElement.ValueKind != JsonValueKind.Object)
            return DPoPValidationResult.Proof("The jwk header is missing.");
        var (publicKey, keyError) = ReadPublicKey(jwkElement);
        if (publicKey is null)
            return DPoPValidationResult.Proof(keyError!);
        var (key, jkt) = publicKey;

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(proof, new TokenValidationParameters
        {
            IssuerSigningKey = key,
            ValidAlgorithms = [jwt.Alg],
            RequireSignedTokens = true,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = false,
            // The proof has no exp claim, and no kid to match
            RequireExpirationTime = false,
            TryAllIssuerSigningKeys = true,
        });
        if (!validation.IsValid)
            return DPoPValidationResult.Proof("The signature of the DPoP proof does not verify with the jwk header.");

        // Rule 3: the required claims
        using var payload = ParsePayload(jwt);
        var claims = payload.RootElement;
        if (!TryGetString(claims, "jti", out var jti) || !TryGetString(claims, "htm", out var htm)
            || !TryGetString(claims, "htu", out var htu) || !TryGetLong(claims, "iat", out var iat))
            return DPoPValidationResult.Proof("The DPoP proof needs the claims jti, htm, htu, and iat.");

        // Rule 8
        if (!string.Equals(htm, request.Method, StringComparison.Ordinal))
            return DPoPValidationResult.Proof("The htm claim does not match the HTTP method.");

        // Rule 9
        if (!UrlsMatch(htu, request.Url))
            return DPoPValidationResult.Proof("The htu claim does not match the HTTP URI.");

        // Rule 11
        var now = timeProvider.GetUtcNow();
        var issued = DateTimeOffset.FromUnixTimeSeconds(iat);
        if (issued < now - options.Value.ProofLifetime || issued > now + options.Value.ClockSkew)
            return DPoPValidationResult.Proof("The iat claim is outside the accepted window.");

        // Rule 12: ath
        if (!TryGetString(claims, "ath", out var ath))
            return DPoPValidationResult.Proof("The DPoP proof needs the ath claim, because it goes with an access token.");
        var expectedAth = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(request.AccessToken)));
        if (!string.Equals(ath, expectedAth, StringComparison.Ordinal))
            return DPoPValidationResult.Proof("The ath claim does not match the access token.");

        // Rule 12: the key of the access token
        if (!string.Equals(jkt, request.AccessTokenJkt, StringComparison.Ordinal))
            return DPoPValidationResult.Token("Invalid DPoP key binding");

        // No replay of jti. Last, so that a proof that fails another check does not use its jti.
        if (!await replayCache.TryAddAsync(jkt, jti, issued + options.Value.ReplayWindow, cancellationToken))
            return DPoPValidationResult.Proof("The jti claim was used before.");

        return DPoPValidationResult.Success(jkt);
    }

    /// <summary>
    /// Reads the public key of a <c>jwk</c> header, and its RFC 7638 thumbprint. The key has only the members of the
    /// thumbprint, so the signature check uses the same key that the thumbprint names. The checks of the member names
    /// ignore case, because IdentityModel reads the members of a JWK without case.
    /// </summary>
    public static (DPoPPublicKey? Key, string? Error) ReadPublicKey(JsonElement jwk)
    {
        foreach (var member in jwk.EnumerateObject())
        {
            if (PrivateKeyMembers.Contains(member.Name, StringComparer.OrdinalIgnoreCase))
                return (null, "The jwk header must not contain a private key.");
            if (KeyReferenceMembers.Contains(member.Name, StringComparer.OrdinalIgnoreCase))
                return (null, "The jwk header must contain only the key, without a certificate.");
        }

        var key = Member(jwk, "kty") switch
        {
            "EC" when Member(jwk, "crv") is { } crv && Member(jwk, "x") is { } x && Member(jwk, "y") is { } y =>
                new JsonWebKey { Kty = "EC", Crv = crv, X = x, Y = y },
            "RSA" when Member(jwk, "n") is { } n && Member(jwk, "e") is { } e =>
                new JsonWebKey { Kty = "RSA", N = n, E = e },
            _ => null,
        };
        if (key is null)
            return (null, "The jwk header must be an EC or an RSA public key.");

        try
        {
            return (new DPoPPublicKey(key, Base64UrlEncoder.Encode(key.ComputeJwkThumbprint())), null);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or CryptographicException)
        {
            return (null, "The jwk header is not a valid key.");
        }
    }

    private static string? Member(JsonElement jwk, string name) =>
        jwk.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>
    /// True when the two URLs are the same without the query and the fragment. The scheme and the host compare
    /// without case, and a default port is the same as no port (RFC 3986 sections 6.2.2 and 6.2.3).
    /// </summary>
    public static bool UrlsMatch(string claim, Uri actual)
    {
        if (!Uri.TryCreate(claim, UriKind.Absolute, out var claimed))
            return false;

        return string.Equals(claimed.GetLeftPart(UriPartial.Path), actual.GetLeftPart(UriPartial.Path), StringComparison.Ordinal);
    }

    /// <summary>
    /// The payload of a token as a JSON document. The token handler does not give a claim as a JSON element for each
    /// type. Parse it once for each check, and dispose it.
    /// </summary>
    public static JsonDocument ParsePayload(JsonWebToken jwt) =>
        JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(jwt.EncodedPayload));

    private static bool TryGetString(JsonElement claims, string name, out string value)
    {
        value = string.Empty;
        if (!claims.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
            return false;

        value = element.GetString() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool TryGetLong(JsonElement claims, string name, out long value)
    {
        value = 0;
        return claims.TryGetProperty(name, out var element)
               && element.ValueKind == JsonValueKind.Number
               && element.TryGetInt64(out value);
    }
}
