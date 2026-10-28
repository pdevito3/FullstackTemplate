using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace FullstackTemplate.AppHost.AuthProviders;

/// <summary>
/// Creates a configuration for 0factor running locally in Docker. 0factor is a mock OIDC provider with no passwords:
/// the login page lists the users, and a click signs in as that user. It reads Keycloak realm exports and gives
/// Keycloak URLs and token shapes. See https://github.com/c4l3b/0factor.
/// Test users: admin@example.com (roles admin and user) and user@example.com (role user). The seed gives them the same
/// IDs as the Keycloak seed, so the sub claim, and with it User.Identifier in the app database, stays the same when you
/// change between Keycloak and ZeroFactor. To sign in as a user from another provider, register it on the login page
/// and enter its sub as the User ID. Manage the users at /_0factor/realms/aspire on the zerofactor endpoint.
/// The realm is lenient: it accepts any redirect URI, so the seed needs no app URLs.
/// Agent clients: fleet-sync gets a client_credentials token for its hidden service account, for a machine agent.
/// clinic-assistant is a public client that gets a token for a test user with the password grant and any password, for
/// a delegated agent. The app treats each as an unknown client until an agent record names it: a caller that manages
/// all tenants binds fleet-sync to a tenant, and a tenant admin registers clinic-assistant.
/// </summary>
public sealed class ZeroFactorAuthProvider : IAuthProvider
{
    public AuthProviderConfig AddTo(IDistributedApplicationBuilder builder, AuthClientUrls clientUrls)
    {
        // 0factor lets anyone who can reach it sign in as any user, so a deployment must never include it
        if (builder.ExecutionContext.IsPublishMode)
            throw new InvalidOperationException(
                $"{nameof(ZeroFactorAuthProvider)} is for local development only. Select another AuthProvider to publish or deploy.");

        const string realmName = "aspire";
        const string clientId = "aspire-app";

        var zeroFactor = builder.AddContainer("zerofactor", "c4l3b/0factor", "0.1.8")
            .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
            // The volume keeps the users that you register and the signing keys
            .WithDataVolumeUnlessIsolated("/data")
            .WithSeedFiles(this, "/opt/keycloak/data/import")
            .WithHttpHealthCheck($"/realms/{realmName}/.well-known/openid-configuration");

        return new AuthProviderConfig
        {
            ClientId = clientId,
            ClientSecret = "zerofactor-accepts-any-secret",
            Audience = clientId,
            RequireHttpsMetadata = false,
            NameClaimType = "email",
            RoleClaimType = "roles",
            AuthResource = zeroFactor,
            AuthorityPathSuffix = $"/realms/{realmName}"
        };
    }
}
