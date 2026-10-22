using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using FullstackTemplate.Bff.Features;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// Revokes one refresh token at the identity provider (RFC 7009, <c>token_type_hint=refresh_token</c>), when
/// revocation is active (<c>Auth:Features:Revocation</c>). If it is not, the revocation is skipped. A failed
/// revocation is logged and does not fail the request.
/// </summary>
public sealed class RefreshTokenRevocation(
    ProviderFeatures features,
    IOpenIdConnectUserTokenEndpoint tokenEndpoint)
{
    private static readonly ILogger Logger = Log.ForContext<RefreshTokenRevocation>();

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (!features.Active.Revocation)
            return;

        try
        {
            // Token management logs an error response of the provider and returns.
            await tokenEndpoint.RevokeRefreshTokenAsync(
                new UserRefreshToken(RefreshToken.Parse(refreshToken), DPoPProofKey: null),
                new UserTokenRequestParameters(),
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            Logger.Here().Warning(ex, "Could not revoke the refresh token {TokenFingerprint}", Fingerprint.Of(refreshToken));
        }
    }
}
