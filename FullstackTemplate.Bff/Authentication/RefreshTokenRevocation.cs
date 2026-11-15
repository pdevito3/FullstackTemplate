using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using FullstackTemplate.Bff.Features;
using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// Revokes one refresh token at the identity provider (RFC 7009, <c>token_type_hint=refresh_token</c>), when
/// revocation is active (<c>Auth:Features:Revocation</c>). If it is not, the revocation is skipped. Each revocation
/// has a limit of <see cref="RefreshOptions.TokenRequestTimeout"/>. A revocation that fails or reaches the limit is
/// logged and does not fail the request.
/// </summary>
public sealed class RefreshTokenRevocation(
    ProviderFeatures features,
    IOpenIdConnectUserTokenEndpoint tokenEndpoint,
    IOptionsMonitor<RefreshOptions> options,
    TimeProvider timeProvider)
{
    private static readonly ILogger Logger = Log.ForContext<RefreshTokenRevocation>();

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (!features.Active.Revocation)
            return;

        var limit = options.CurrentValue.TokenRequestTimeout;
        using var timeout = new CancellationTokenSource(limit, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            // Token management logs an error response of the provider and returns.
            await tokenEndpoint.RevokeRefreshTokenAsync(
                new UserRefreshToken(RefreshToken.Parse(refreshToken), DPoPProofKey: null),
                new UserTokenRequestParameters(),
                linked.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            Logger.Here().Warning("Could not revoke the refresh token {TokenFingerprint} within {Limit}",
                Fingerprint.Of(refreshToken), limit);
        }
        // Token management throws InvalidOperationException when it cannot load the discovery document, also at the
        // limit
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            Logger.Here().Warning(ex, "Could not revoke the refresh token {TokenFingerprint}", Fingerprint.Of(refreshToken));
        }
    }
}
