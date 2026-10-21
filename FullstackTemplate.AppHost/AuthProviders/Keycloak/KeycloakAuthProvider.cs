using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace FullstackTemplate.AppHost.AuthProviders;

/// <summary>
/// Creates a configuration for Keycloak running locally in Docker.
/// Uses PostgreSQL database with data volume for persistence and realm import for auto-provisioning.
/// Test users: admin@example.com (roles admin and user) and user@example.com (role user). The password is password123!.
/// </summary>
public sealed class KeycloakAuthProvider : IAuthProvider
{
    public AuthProviderConfig AddTo(IDistributedApplicationBuilder builder, AuthClientUrls clientUrls)
    {
        const string realmName = "aspire";
        const string clientId = "aspire-app";

        var keycloakPostgres = builder.AddPostgres("keycloak-postgres")
            .WithImageTag(Config.PostgresImageTag)
            .WithDataVolumeUnlessIsolated();
        var keycloakDb = keycloakPostgres.AddDatabase("keycloakdb");

        var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.0")
            .WithReference(keycloakDb)
            .WaitFor(keycloakDb)
            .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
            .WithEnvironment("KEYCLOAK_ADMIN", "admin")
            .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
            .WithEnvironment("KC_HEALTH_ENABLED", "true")
            .WithEnvironment("KC_HTTP_ENABLED", "true")
            .WithEnvironment("KC_DB", "postgres")
            .WithEnvironment(context =>
            {
                // Configure Keycloak to connect to PostgreSQL
                var pgResource = keycloakDb.Resource.Parent;
                context.EnvironmentVariables["KC_DB_URL"] = ReferenceExpression.Create(
                    $"jdbc:postgresql://{pgResource.PrimaryEndpoint.Property(EndpointProperty.Host)}:{pgResource.PrimaryEndpoint.Property(EndpointProperty.Port)}/{keycloakDb.Resource.DatabaseName}");
                context.EnvironmentVariables["KC_DB_USERNAME"] = pgResource.UserNameReference;
                context.EnvironmentVariables["KC_DB_PASSWORD"] = pgResource.PasswordParameter;
            })
            .WithSeedFiles(this, "/opt/keycloak/data/import")
            .WithClientUrls(clientUrls)
            .WithArgs("start-dev", "--import-realm")
            .WaitFor(keycloakDb)
            // Health check ensures Keycloak and the realm are ready before server starts
            .WithHttpHealthCheck($"/realms/{realmName}/.well-known/openid-configuration");

        keycloakPostgres.WithParentRelationship(keycloak);

        return new AuthProviderConfig
        {
            ClientId = clientId,
            ClientSecret = "super-secret-client-secret-change-in-production",
            Audience = clientId,
            RequireHttpsMetadata = false,
            NameClaimType = "email",
            RoleClaimType = "roles",
            AuthResource = keycloak,
            AuthorityPathSuffix = $"/realms/{realmName}"
        };
    }
}
