using Duende.AccessTokenManagement.OpenIdConnect;
using FullstackTemplate.Bff.DPoP;
using FullstackTemplate.Bff.Login;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Features;

/// <summary>
/// The active features of this BFF. <see cref="ProviderFeatureDiscovery"/> decides them at startup, before the
/// server takes requests.
/// </summary>
public sealed class ProviderFeatures
{
    private ActiveFeatures? _active;

    public ActiveFeatures Active =>
        _active ?? throw new InvalidOperationException("The provider features are not decided yet. ProviderFeatureDiscovery decides them at startup.");

    public void Set(ActiveFeatures active) => _active = active;
}

/// <summary>
/// Reads the discovery document one time at startup through the <c>ConfigurationManager</c> of the OIDC handler,
/// and decides the active features. If the read fails, or a <c>Require</c> setting is not met, the BFF stops.
/// The endpoint URLs still come from the live <c>ConfigurationManager</c>.
/// </summary>
public sealed class ProviderFeatureDiscovery(
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
    IOptions<ProviderFeatureOptions> featureOptions,
    IConfiguration configuration,
    ProviderFeatures features,
    TokenClientAuthentication tokenClientAuthentication,
    IOptions<UserTokenManagementOptions> tokenManagementOptions) : IHostedService
{
    private static readonly ILogger Logger = Log.ForContext<ProviderFeatureDiscovery>();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Source.StartActivity("provider_features.decide");
        try
        {
            var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
            var discovery = options.ConfigurationManager is { } manager
                ? await manager.GetConfigurationAsync(cancellationToken)
                : options.Configuration ?? throw new InvalidOperationException("The OIDC handler has no discovery document.");

            var clientAuthentication = configuration.GetValue("Auth:ClientAuthentication", ClientAuthenticationMethod.Auto);
            var (active, warnings) = ProviderFeatureDecision.Decide(discovery, featureOptions.Value, clientAuthentication,
                configuration["Auth:LogoutEndpoint"],
                tokenManagementOptions.Value.DPoPJsonWebKey is { } dpopKey ? DPoPKey.Algorithm(dpopKey) : null);

            foreach (var warning in warnings)
            {
                Logger.Here().Warning(
                    "The identity provider lacks the feature {Feature}: the discovery document does not list {DiscoveryField}. {Fallback}",
                    warning.Feature, warning.DiscoveryField, warning.Fallback);
            }

            features.Set(active);
            tokenClientAuthentication.Apply(active.ClientAuthentication);
            // The registration set the key when the setting is not Disable. Under Auto, discovery can still lack DPoP.
            if (!active.DPoP)
                tokenManagementOptions.Value.DPoPJsonWebKey = null;

            Logger.Here().Information(
                "Decided the provider features from the discovery document of {Issuer}: Revocation={Revocation}, IssuerParameter={IssuerParameter}, BackChannelLogout={BackChannelLogout}, EndSession={EndSession}, PushedAuthorization={PushedAuthorization}, DPoP={DPoP}, ClientAuthentication={ClientAuthentication}",
                active.Issuer, active.Revocation, active.IssuerParameter, active.BackChannelLogout, active.EndSession,
                active.PushedAuthorization, active.DPoP, active.ClientAuthentication);
        }
        catch (Exception ex)
        {
            Telemetry.RecordError(activity, ex);
            Logger.Here().Error(ex, "Could not decide the provider features from the discovery document");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
