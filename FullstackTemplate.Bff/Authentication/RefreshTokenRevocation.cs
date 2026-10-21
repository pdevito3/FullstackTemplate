using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Authentication;

public sealed class RevocationOptions
{
    /// <summary>
    /// Revoke refresh tokens at the identity provider (RFC 7009). From <c>Auth:RevokeRefreshTokenOnLogout</c>,
    /// default true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Revokes one refresh token at the identity provider (RFC 7009, <c>token_type_hint=refresh_token</c>). A
/// provider with no <c>revocation_endpoint</c> in discovery is skipped. A failed revocation is logged and does
/// not fail the request.
/// </summary>
public sealed class RefreshTokenRevocation(
    IOptions<RevocationOptions> options,
    IOpenIdConnectConfigurationService oidcConfiguration,
    IOpenIdConnectUserTokenEndpoint tokenEndpoint)
{
    private static readonly ILogger Logger = Log.ForContext<RefreshTokenRevocation>();

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return;

        var configuration = await oidcConfiguration.GetOpenIdConnectConfigurationAsync(ct: cancellationToken);
        if (configuration.RevocationEndpoint is null)
        {
            Logger.Here().Information("Skipped the refresh token revocation, because discovery has no revocation_endpoint");
            return;
        }

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
