using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// Limits each refresh call to the token endpoint to <see cref="RefreshOptions.TokenRequestTimeout"/>. The
/// refresh lock holds a <c>bffdb</c> connection and the advisory lock during the call, so a hung identity provider
/// must not hold them until the timeout of the backchannel. A call that reaches the limit gives the temporary
/// failure <see cref="TimeoutError"/>, so the failure rules of the token attach apply.
/// </summary>
public sealed class RefreshTimeout(IOpenIdConnectUserTokenEndpoint inner, IOptionsMonitor<RefreshOptions> options)
    : IOpenIdConnectUserTokenEndpoint
{
    /// <summary>
    /// The error of a refresh call that reached <see cref="RefreshOptions.TokenRequestTimeout"/>.
    /// </summary>
    public const string TimeoutError = "bff_refresh_timeout";

    public async Task<TokenResult<UserToken>> RefreshAccessTokenAsync(UserRefreshToken userToken,
        UserTokenRequestParameters parameters, CancellationToken ct = default)
    {
        var limit = options.CurrentValue.TokenRequestTimeout;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(limit);
        try
        {
            return await inner.RefreshAccessTokenAsync(userToken, parameters, timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return TokenResult.Failure(TimeoutError, $"The token endpoint did not answer within {limit}");
        }
    }

    public Task RevokeRefreshTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters,
        CancellationToken ct = default) =>
        inner.RevokeRefreshTokenAsync(userToken, parameters, ct);
}
