using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FullstackTemplate.AppHost.AuthProviders;

/// <summary>
/// Creates a configuration for Pocket ID running locally in Docker. Pocket ID is an OIDC provider with passkeys and no
/// passwords. See https://pocket-id.org.
/// The AppHost seeds Pocket ID through its admin API with <see cref="PocketIdSeeder"/>, not with seed files: the
/// image has no curl and no jq, and its busybox wget cannot send PUT. The seed is the desired state, so each start
/// creates or updates each item.
/// Test users: admin@example.com (groups admin and user) and user@example.com (group user). The seed gives them the
/// same IDs as the Keycloak and ZeroFactor seeds, so the sub claim stays the same when you change between these
/// providers.
/// A new database has no passkey. On each start, the console log of the pocketid resource has a login link for each
/// test user. The command "Print login links" of the resource prints new ones. Open a link to sign in, and then open
/// the app.
/// </summary>
/// <remarks>
/// Pocket ID is different from the other providers in these ways:
/// <list type="bullet">
///   <item>The ID token and the access token have the same aud, the client ID. The ID token has the claim
///   <c>type</c> with the value <c>id-token</c>, and the server refuses such a token.</item>
///   <item>The access token has only sub, client_id, and the scopes. It has no email and no groups.</item>
///   <item>A refresh gives a new refresh token, but the old one still works for one minute.</item>
///   <item>The end_session_endpoint needs an id_token_hint. Without one, Pocket ID shows its own logout page.</item>
///   <item>It has no revocation_endpoint and no DPoP.</item>
/// </list>
/// </remarks>
public sealed class PocketIdAuthProvider : IAuthProvider
{
    private const string ImageTag = "v2.19.3";
    private const string SeedHealthCheck = "pocketid-seed";

    public AuthProviderConfig AddTo(IDistributedApplicationBuilder builder, AuthClientUrls clientUrls)
    {
        // Pocket ID encrypts its data with this key, so the value must stay the same while the data volume stays
        var encryptionKey = builder.AddParameter("pocketid-encryption-key", new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true, persist: true);
        // The admin API key. The seeder and the back-channel logout e2e test send it in X-API-KEY.
        var staticApiKey = builder.AddParameter("pocketid-static-api-key", new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true, persist: true);

        var pocketId = builder.AddContainer("pocketid", "ghcr.io/pocket-id/pocket-id", ImageTag)
            .WithHttpEndpoint(port: 24790, targetPort: 1411, name: "http")
            // The volume keeps the SQLite database, with the signing keys and the passkeys that you add
            .WithDataVolumeUnlessIsolated("/app/data")
            .WithEnvironment("ENCRYPTION_KEY", encryptionKey)
            .WithEnvironment("STATIC_API_KEY", staticApiKey)
            .WithEnvironment("ANALYTICS_DISABLED", "true")
            .WithEnvironment("VERSION_CHECK_DISABLED", "true")
            // Pocket ID takes 5 login codes from one address, and then one each 10 seconds. The e2e tests sign in with
            // a login code more often than that. The container is for local development only.
            .WithEnvironment("DISABLE_RATE_LIMITING", "true")
            // Pocket ID sends a logout token only to a public address, unless this list allows the address. The BFF
            // has a private address in the container network.
            .WithEnvironment("OUTBOUND_ALLOWED_HOSTS_BACKCHANNEL_LOGOUT", "private")
            // Pocket ID does not read these variables, but the reference to the container view of the BFF makes Aspire
            // open the container tunnel to the BFF. Without it, the back-channel URL of the seed never resolves.
            .WithClientUrls(clientUrls)
            .WithHealthCheck(SeedHealthCheck);
        var pocketIdHttp = pocketId.GetEndpoint("http");
        // APP_URL is the issuer and the WebAuthn relying party. It must be the URL that the browser, the BFF, and the
        // server use (the host view), because the BFF checks that the issuer is the authority.
        pocketId.WithEnvironment(context => context.EnvironmentVariables["APP_URL"] = pocketIdHttp.Url);

        var seeder = new PocketIdSeeder(pocketIdHttp, staticApiKey.Resource, clientUrls);
        // The BFF and the server wait for this check. It passes only when the seed is complete.
        builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            SeedHealthCheck,
            services => new PocketIdSeedHealthCheck(seeder, services.GetRequiredService<ResourceLoggerService>().GetLogger(pocketId.Resource)),
            failureStatus: null,
            tags: null,
            // A seed that hangs shows as an unhealthy check, not as a check that never ends
            timeout: TimeSpan.FromSeconds(30)));
        pocketId.WithCommand("login-links", "Print login links", async context =>
        {
            var log = context.Services.GetRequiredService<ResourceLoggerService>().GetLogger(pocketId.Resource);
            await seeder.PrintLoginLinksAsync(log, context.CancellationToken);
            return CommandResults.Success();
        }, new CommandOptions
        {
            Description = "Prints a new login link for each test user to the console log of pocketid",
            IconName = "Key",
            UpdateState = context => context.ResourceSnapshot.HealthStatus == HealthStatus.Healthy
                ? ResourceCommandState.Enabled
                : ResourceCommandState.Disabled,
        });

        return new AuthProviderConfig
        {
            ClientId = PocketIdSeeder.ClientId,
            ClientSecret = PocketIdSeeder.ClientSecret,
            Audience = PocketIdSeeder.ClientId,
            RequireHttpsMetadata = false,
            NameClaimType = "email",
            RoleClaimType = "groups",
            // The access token has the client ID as its aud with no extra scope, and Pocket ID does not know the scope
            UseAudienceAsScope = false,
            AuthResource = pocketId,
            OperatorSubject = PocketIdSeeder.AdminUserId
        };
    }
}
