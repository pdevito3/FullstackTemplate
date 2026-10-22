using System.Globalization;
using System.Security.Claims;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using FullstackTemplate.Bff.Features;
using FullstackTemplate.Bff.Login;
using FullstackTemplate.Bff.Sessions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// The checks of an <c>id_token</c> in a refresh response (OIDC Core 12.2). It wraps the token endpoint of token
/// management, so it sees each refresh response before token management stores it.
/// <para>
/// The ID token gets the checks of the login ID token: a signature with the provider keys and the algorithm of the
/// client registration, <c>iss</c> equal to the discovery issuer, <c>aud</c> with only the client id, an
/// <c>azp</c> equal to the client id when present, <c>exp</c>, and a present <c>iat</c>. It needs no nonce. Its
/// <c>sub</c> must equal the <c>sub</c> of the BFF session, and its <c>auth_time</c> must equal the one of the
/// BFF session when both have one. A refresh response with no <c>id_token</c> passes.
/// </para>
/// <para>
/// A failed check fails the refresh with <see cref="InvalidIdTokenError"/>, so token management does not store
/// the new tokens. The error is permanent: the refresh lock ends the BFF session, as for <c>invalid_grant</c>. A
/// wrong ID token is a sign of a wrong or attacked identity provider, so a retry must not use it. The new refresh
/// token is revoked, when revocation is active, because no BFF session holds it.
/// </para>
/// </summary>
public sealed class RefreshIdTokenValidation(
    IOpenIdConnectUserTokenEndpoint inner,
    IHttpContextAccessor httpContextAccessor,
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
    ProviderFeatures features) : IOpenIdConnectUserTokenEndpoint
{
    /// <summary>
    /// The error of a refresh whose response has an ID token that fails the checks. It ends the BFF session.
    /// </summary>
    public const string InvalidIdTokenError = "bff_invalid_refresh_id_token";

    private const string AuthTimeClaim = "auth_time";

    private static readonly ILogger Logger = Log.ForContext<RefreshIdTokenValidation>();

    public async Task<TokenResult<UserToken>> RefreshAccessTokenAsync(UserRefreshToken userToken,
        UserTokenRequestParameters parameters, CancellationToken ct = default)
    {
        var result = await inner.RefreshAccessTokenAsync(userToken, parameters, ct);
        if (!result.WasSuccessful(out var token) || token.IdentityToken is not { } idToken)
            return result;

        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("The refresh ID token check needs the HttpContext of the request.");
        var failure = await CheckAsync(idToken.ToString(), httpContext.User, ct);
        if (failure is null)
            return result;

        var sessionKey = httpContext.RequestServices.GetRequiredService<BffSessionRequest>().Key;
        Logger.Here().Warning("Rejected the ID token {IdTokenFingerprint} of the refresh of the BFF session {SessionKey}: {Reason}",
            Fingerprint.Of(idToken.ToString()), sessionKey is null ? null : Fingerprint.Of(sessionKey), failure);

        if (token.RefreshToken is { } refreshToken)
            await new RefreshTokenRevocation(features, inner).RevokeAsync(refreshToken.ToString(), ct);

        return TokenResult.Failure(InvalidIdTokenError, failure);
    }

    /// <summary>
    /// Wraps the token endpoint that token management registered. Call it right after
    /// <c>AddOpenIdConnectAccessTokenManagement</c>, so that it is the innermost wrapper.
    /// </summary>
    public static void Register(IServiceCollection services) =>
        TokenEndpointDecorator.Decorate(services, (provider, inner) => new RefreshIdTokenValidation(
            inner,
            provider.GetRequiredService<IHttpContextAccessor>(),
            provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>(),
            provider.GetRequiredService<ProviderFeatures>()));

    public Task RevokeRefreshTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters,
        CancellationToken ct = default) =>
        inner.RevokeRefreshTokenAsync(userToken, parameters, ct);

    /// <summary>
    /// The checks of <paramref name="idToken"/> against the BFF session <paramref name="session"/>. Null when it
    /// passes, or the reason, with no token value.
    /// </summary>
    private async Task<string?> CheckAsync(string idToken, ClaimsPrincipal session, CancellationToken ct)
    {
        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        var result = await ValidateAsync(idToken, options, ct);
        if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt)
            return $"the ID token failed validation: {result.Exception?.GetType().Name ?? "unknown error"}";

        if (IdTokenChecks.CheckAudience(jwt.Audiences, options.ClientId!) is { } audienceFailure)
            return audienceFailure;

        // Core 3.1.3.7 step 5
        if (jwt.TryGetPayloadValue<string>("azp", out var azp) && !string.Equals(azp, options.ClientId, StringComparison.Ordinal))
            return "the azp of the ID token is not the client id";

        if (!jwt.TryGetPayloadValue<object>("iat", out _))
            return "the ID token has no iat";

        var sessionSubject = session.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(sessionSubject) || !string.Equals(jwt.Subject, sessionSubject, StringComparison.Ordinal))
            return "the sub of the ID token is not the sub of the BFF session";

        if (long.TryParse(session.FindFirst(AuthTimeClaim)?.Value, CultureInfo.InvariantCulture, out var sessionAuthTime)
            && jwt.TryGetPayloadValue<long>(AuthTimeClaim, out var authTime)
            && authTime != sessionAuthTime)
            return "the auth_time of the ID token is not the auth_time of the BFF session";

        return null;
    }

    /// <summary>
    /// The signature, <c>iss</c>, <c>aud</c>, and lifetime checks. An unknown key refreshes the signing keys one
    /// time and validates again, as at the login (Core 10.1.1).
    /// </summary>
    private static async Task<TokenValidationResult> ValidateAsync(string idToken, OpenIdConnectOptions options, CancellationToken ct)
    {
        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(idToken, await ParametersAsync(options, ct));
        if (result.Exception is not SecurityTokenSignatureKeyNotFoundException || options.ConfigurationManager is null)
            return result;

        options.ConfigurationManager.RequestRefresh();
        return await handler.ValidateTokenAsync(idToken, await ParametersAsync(options, ct));
    }

    private static async Task<TokenValidationParameters> ParametersAsync(OpenIdConnectOptions options, CancellationToken ct)
    {
        var configuration = options.Configuration
            ?? (options.ConfigurationManager is { } manager ? await manager.GetConfigurationAsync(ct) : null)
            ?? throw new InvalidOperationException("The OIDC handler has no discovery document.");
        return new TokenValidationParameters
        {
            ValidIssuer = configuration.Issuer,
            ValidAudience = options.ClientId,
            IssuerSigningKeys = configuration.SigningKeys,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidAlgorithms = options.TokenValidationParameters.ValidAlgorithms,
        };
    }
}
