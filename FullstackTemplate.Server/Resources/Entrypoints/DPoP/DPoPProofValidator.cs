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
        if (PrivateKeyMembers.Any(member => jwkElement.TryGetProperty(member, out _)))
            return DPoPValidationResult.Proof("The jwk header must not contain a private key.");

        JsonWebKey key;
        string jkt;
        try
        {
            key = new JsonWebKey(jwkElement.GetRawText());
            if (key.Kty is not ("EC" or "RSA"))
                return DPoPValidationResult.Proof("The jwk header must be an EC or an RSA key.");
            jkt = Base64UrlEncoder.Encode(key.ComputeJwkThumbprint());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or JsonException or CryptographicException)
        {
            return DPoPValidationResult.Proof("The jwk header is not a valid key.");
        }

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
        if (!TryGetString(jwt, "jti", out var jti) || !TryGetString(jwt, "htm", out var htm)
            || !TryGetString(jwt, "htu", out var htu) || !TryGetLong(jwt, "iat", out var iat))
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
        if (!TryGetString(jwt, "ath", out var ath))
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
    /// The payload of a token as a JSON object. The token handler does not give a claim as a JSON element for each type.
    /// </summary>
    public static JsonElement PayloadOf(JsonWebToken jwt) =>
        JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(jwt.EncodedPayload)).RootElement;

    private static bool TryGetString(JsonWebToken jwt, string name, out string value)
    {
        value = string.Empty;
        if (!PayloadOf(jwt).TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
            return false;

        value = element.GetString() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool TryGetLong(JsonWebToken jwt, string name, out long value)
    {
        value = 0;
        return PayloadOf(jwt).TryGetProperty(name, out var element)
               && element.ValueKind == JsonValueKind.Number
               && element.TryGetInt64(out value);
    }
}
