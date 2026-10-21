using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FullstackTemplate.Bff.Management;

/// <summary>
/// Reads the discovery document through the <c>ConfigurationManager</c> of the OIDC handler, so that the
/// management endpoints and the handler share one cache of the document and the signing keys.
/// </summary>
public static class OidcDiscovery
{
    /// <summary>
    /// The discovery document, or null when the OIDC options have no configuration and no configuration manager.
    /// </summary>
    public static async Task<OpenIdConnectConfiguration?> GetConfigurationAsync(
        IOptionsMonitor<OpenIdConnectOptions> optionsMonitor,
        CancellationToken cancellationToken)
    {
        var options = optionsMonitor.Get(OpenIdConnectDefaults.AuthenticationScheme);
        if (options.Configuration is not null)
            return options.Configuration;

        return options.ConfigurationManager is null
            ? null
            : await options.ConfigurationManager.GetConfigurationAsync(cancellationToken);
    }
}
