using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// Adds each failed refresh to <c>bff.refresh.failures</c>. It wraps the token endpoint of token management, so
/// it sees each refresh call to the identity provider. A request that needs no refresh, or a BFF session with no
/// refresh token, is not a refresh failure.
/// </summary>
public sealed class RefreshFailureMetrics(IOpenIdConnectUserTokenEndpoint inner) : IOpenIdConnectUserTokenEndpoint
{
    public async Task<TokenResult<UserToken>> RefreshAccessTokenAsync(UserRefreshToken userToken,
        UserTokenRequestParameters parameters, CancellationToken ct = default)
    {
        TokenResult<UserToken> result;
        try
        {
            result = await inner.RefreshAccessTokenAsync(userToken, parameters, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            Telemetry.RecordRefreshFailure(RefreshFailureKind.Temporary);
            throw;
        }

        if (!result.WasSuccessful(out _, out var failure))
            Telemetry.RecordRefreshFailure(KindOf(failure.Error));

        return result;
    }

    public Task RevokeRefreshTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters,
        CancellationToken ct = default) =>
        inner.RevokeRefreshTokenAsync(userToken, parameters, ct);

    /// <summary>
    /// <c>invalid_grant</c> is permanent: the refresh token is expired, revoked, or used. A rejected ID token in the
    /// refresh response (<see cref="RefreshIdTokenValidation.InvalidIdTokenError"/>) is also permanent. Each other
    /// error, such as a network error, a timeout, or a 5xx, is temporary.
    /// </summary>
    public static string KindOf(string? error) =>
        string.Equals(error, "invalid_grant", StringComparison.Ordinal)
        || string.Equals(error, RefreshIdTokenValidation.InvalidIdTokenError, StringComparison.Ordinal)
            ? RefreshFailureKind.Permanent
            : RefreshFailureKind.Temporary;

    /// <summary>
    /// Wraps the token endpoint that token management registered, with the <see cref="RefreshTimeout"/> inside, so
    /// that a timeout counts as a temporary failure. Call it after <see cref="RefreshIdTokenValidation.Register"/>,
    /// so that a rejected ID token counts as a failure.
    /// </summary>
    public static void Register(IServiceCollection services) =>
        TokenEndpointDecorator.Decorate(services, (provider, inner) => new RefreshFailureMetrics(new RefreshTimeout(
            inner, provider.GetRequiredService<IOptionsMonitor<RefreshOptions>>())));
}
