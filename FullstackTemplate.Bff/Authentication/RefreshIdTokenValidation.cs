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
/// <para>
/// The checks and the revocation do not use the token of the refresh call. <see cref="RefreshTimeout"/> cancels
/// it at <see cref="RefreshOptions.TokenRequestTimeout"/>, and a cancellation gives the temporary
/// <see cref="RefreshTimeout.TimeoutError"/>, so the BFF session would stay. The checks and the revocation each get
/// their own limit of <see cref="RefreshOptions.TokenRequestTimeout"/>. Signing keys that do not load within the
/// limit fail the checks. A revocation that does not end within the limit is logged. Thus a request that waits for
/// this refresh can reach <see cref="RefreshOptions.LockTimeout"/> and get a temporary failure, but the refresh
/// lock still ends the BFF session.
/// </para>
/// </summary>
public sealed class RefreshIdTokenValidation(
    IOpenIdConnectUserTokenEndpoint inner,
    IHttpContextAccessor httpContextAccessor,
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
    IOptionsMonitor<RefreshOptions> refreshOptions,
    ProviderFeatures features,
    TimeProvider timeProvider) : IOpenIdConnectUserTokenEndpoint
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
        var limit = refreshOptions.CurrentValue.TokenRequestTimeout;
        var failure = await CheckAsync(idToken.ToString(), httpContext.User, limit);
        if (failure is null)
            return result;

        var sessionKey = httpContext.RequestServices.GetRequiredService<BffSessionRequest>().Key;
        Logger.Here().Warning("Rejected the ID token {IdTokenFingerprint} of the refresh of the BFF session {SessionKey}: {Reason}",
            Fingerprint.Of(idToken.ToString()), sessionKey is null ? null : Fingerprint.Of(sessionKey), failure);

        if (token.RefreshToken is { } refreshToken)
            await RevokeAsync(refreshToken.ToString(), limit);

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
            provider.GetRequiredService<IOptionsMonitor<RefreshOptions>>(),
            provider.GetRequiredService<ProviderFeatures>(),
            provider.GetRequiredService<TimeProvider>()));

    public Task RevokeRefreshTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters,
        CancellationToken ct = default) =>
        inner.RevokeRefreshTokenAsync(userToken, parameters, ct);

    /// <summary>
    /// Revokes the refresh token of a rejected refresh response, with a limit of <paramref name="limit"/>. A
    /// revocation that fails or does not end within the limit is logged and does not change the result of the
    /// refresh.
    /// </summary>
    private async Task RevokeAsync(string refreshToken, TimeSpan limit)
    {
        using var timeout = new CancellationTokenSource(limit, timeProvider);
        try
        {
            await new RefreshTokenRevocation(features, inner).RevokeAsync(refreshToken, timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            Logger.Here().Warning("Could not revoke the refresh token {TokenFingerprint} within {Limit}",
                Fingerprint.Of(refreshToken), limit);
        }
        catch (InvalidOperationException ex)
        {
            // Token management throws it when it cannot load the discovery document, also at the limit
            Logger.Here().Warning(ex, "Could not revoke the refresh token {TokenFingerprint}", Fingerprint.Of(refreshToken));
        }
    }

    /// <summary>
    /// The checks of <paramref name="idToken"/> against the BFF session <paramref name="session"/>. Null when it
    /// passes, or the reason, with no token value. A load of the signing keys that takes longer than
    /// <paramref name="limit"/> fails the checks.
    /// </summary>
    private async Task<string?> CheckAsync(string idToken, ClaimsPrincipal session, TimeSpan limit)
    {
        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        // The limit starts before the load of the signing keys, so it covers the whole load
        using var timeout = new CancellationTokenSource(limit, timeProvider);
        // The load of the signing keys does not stop at a cancellation, so the wait stops instead
        var validation = ValidateAsync(idToken, options);
        TokenValidationResult result;
        try
        {
            result = await validation.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            // Observed here, so that a later failure of the load does not raise UnobservedTaskException
            _ = validation.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return $"the signing keys did not load within {limit}";
        }

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
    private static async Task<TokenValidationResult> ValidateAsync(string idToken, OpenIdConnectOptions options)
    {
        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(idToken, await ParametersAsync(options));
        if (result.Exception is not SecurityTokenSignatureKeyNotFoundException || options.ConfigurationManager is null)
            return result;

        options.ConfigurationManager.RequestRefresh();
        return await handler.ValidateTokenAsync(idToken, await ParametersAsync(options));
    }

    private static async Task<TokenValidationParameters> ParametersAsync(OpenIdConnectOptions options)
    {
        var configuration = options.Configuration
            ?? (options.ConfigurationManager is { } manager ? await manager.GetConfigurationAsync(CancellationToken.None) : null)
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
