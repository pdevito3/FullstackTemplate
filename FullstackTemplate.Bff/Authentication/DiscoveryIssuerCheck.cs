using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// OIDC Discovery 4.3: the <c>issuer</c> of the discovery document must be the issuer URL that the BFF used to find
/// the document, which is <c>Auth:Authority</c>. The OIDC handler does not check it. Each read of the document gets
/// this check, at startup and on each refresh. A failed read at startup stops the BFF. A failed refresh keeps the
/// last good document. IdentityModel hides the reason as PII, so the check logs it.
/// </summary>
public sealed class DiscoveryIssuerCheck(string authority) : IConfigurationValidator<OpenIdConnectConfiguration>
{
    private static readonly ILogger Logger = Log.ForContext<DiscoveryIssuerCheck>();

    /// <summary>
    /// Replaces the <c>ConfigurationManager</c> that the OIDC handler made with one that has this check. It keeps
    /// the address, the backchannel, and the refresh intervals. Run it after the post-configure of the OIDC handler.
    /// </summary>
    public static void Install(OpenIdConnectOptions options)
    {
        if (options.ConfigurationManager is not ConfigurationManager<OpenIdConnectConfiguration> handlerManager)
            return;

        options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            handlerManager.MetadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(options.Backchannel) { RequireHttps = options.RequireHttpsMetadata },
            new DiscoveryIssuerCheck(options.Authority!))
        {
            RefreshInterval = handlerManager.RefreshInterval,
            AutomaticRefreshInterval = handlerManager.AutomaticRefreshInterval,
        };
    }

    public ConfigurationValidationResult Validate(OpenIdConnectConfiguration configuration)
    {
        if (Matches(configuration.Issuer, authority))
            return new ConfigurationValidationResult { Succeeded = true };

        Logger.Here().Error(
            "Rejected the discovery document: its issuer {DiscoveryIssuer} is not Auth:Authority {Authority}. OIDC Discovery 4.3 requires them to be identical",
            configuration.Issuer, authority);
        return new ConfigurationValidationResult
        {
            Succeeded = false,
            ErrorMessage = $"The discovery document has the issuer '{configuration.Issuer}', but Auth:Authority is '{authority}'.",
        };
    }

    /// <summary>
    /// Ordinal compare. A trailing slash on only one side is allowed, because the setting is often written without
    /// the slash of the issuer.
    /// </summary>
    public static bool Matches(string? issuer, string authority) =>
        issuer is { Length: > 0 } && string.Equals(issuer.TrimEnd('/'), authority.TrimEnd('/'), StringComparison.Ordinal);
}
