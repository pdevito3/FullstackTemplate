using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace FullstackTemplate.AppHost.AuthProviders;

/// <summary>
/// Creates a configuration for Authentik running locally in Docker.
/// Sets up PostgreSQL, server, and worker containers with blueprint auto-provisioning.
/// Authentik keeps its cache, sessions, and tasks in PostgreSQL since 2025.10, so it has no Redis.
/// Test users: admin@example.com (roles admin and user) and user@example.com (role user). The password is password123!.
/// The Authentik admin is akadmin@example.com, with the password password123!.
/// </summary>
public sealed class AuthentikAuthProvider : IAuthProvider
{
    // The image of the server and the worker
    private const string ImageTag = "2026.8.5";

    public AuthProviderConfig AddTo(IDistributedApplicationBuilder builder, AuthClientUrls clientUrls)
    {
        const string appSlug = "aspire-app";
        const string clientId = "aspire-app";

        var authentikSecretKey = builder.AddParameter("authentik-secret-key", new GenerateParameterDefault { MinLength = 50, Special = false }, secret: true, persist: true);

        // PostgreSQL database for Authentik
        var authentikDbPassword = builder.AddParameter("authentik-db-password", new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true, persist: true);
        var authentikPostgres = builder.AddPostgres("authentik-postgres", password: authentikDbPassword)
            .WithImageTag(Config.PostgresImageTag)
            .WithDataVolumeUnlessIsolated();
        var authentikDb = authentikPostgres.AddDatabase("authentik-db");

        var postgresEndpoint = authentikPostgres.GetEndpoint("tcp", KnownNetworkIdentifiers.DefaultAspireContainerNetwork);

        // Authentik Worker (must start before server to apply blueprints). It also sends the back-channel logout tokens.
        var worker = builder.AddContainer("authentik-worker", "ghcr.io/goauthentik/server", ImageTag)
            .WithReference(authentikDb)
            .WithEnvironment("AUTHENTIK_SECRET_KEY", authentikSecretKey)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["AUTHENTIK_POSTGRESQL__HOST"] = postgresEndpoint.Property(EndpointProperty.Host);
                context.EnvironmentVariables["AUTHENTIK_POSTGRESQL__PORT"] = postgresEndpoint.Property(EndpointProperty.Port);
            })
            .WithEnvironment("AUTHENTIK_POSTGRESQL__USER", "postgres")
            .WithEnvironment("AUTHENTIK_POSTGRESQL__NAME", "authentik-db")
            .WithEnvironment("AUTHENTIK_POSTGRESQL__PASSWORD", authentikDbPassword)
            .WithEnvironment("AUTHENTIK_BOOTSTRAP_PASSWORD", "password123!")
            .WithEnvironment("AUTHENTIK_BOOTSTRAP_EMAIL", "akadmin@example.com")
            .WithSeedFiles(this, "/blueprints/custom")
            .WithClientUrls(clientUrls)
            .WithArgs("worker")
            .WaitFor(authentikDb);

        // Authentik Server (parent resource for dashboard grouping)
        var server = builder.AddContainer("authentik", "ghcr.io/goauthentik/server", ImageTag)
            .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "http")
            .WithReference(authentikDb)
            .WithEnvironment("AUTHENTIK_SECRET_KEY", authentikSecretKey)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["AUTHENTIK_POSTGRESQL__HOST"] = postgresEndpoint.Property(EndpointProperty.Host);
                context.EnvironmentVariables["AUTHENTIK_POSTGRESQL__PORT"] = postgresEndpoint.Property(EndpointProperty.Port);
            })
            .WithEnvironment("AUTHENTIK_POSTGRESQL__USER", "postgres")
            .WithEnvironment("AUTHENTIK_POSTGRESQL__NAME", "authentik-db")
            .WithEnvironment("AUTHENTIK_POSTGRESQL__PASSWORD", authentikDbPassword)
            .WithArgs("server")
            .WaitFor(worker)
            // Health check ensures Authentik OAuth provider is ready before server starts
            // Using OIDC discovery endpoint instead of /-/health/live/ which only checks if server is up
            .WithHttpHealthCheck($"/application/o/{appSlug}/.well-known/openid-configuration");

        // Group all Authentik resources under the server in the dashboard
        authentikPostgres.WithParentRelationship(server);
        worker.WithParentRelationship(server);

        return new AuthProviderConfig
        {
            ClientId = clientId,
            ClientSecret = "super-secret-client-secret-change-in-production",
            Audience = clientId,
            RequireHttpsMetadata = false,
            NameClaimType = "email",
            RoleClaimType = "roles",
            AuthResource = server,
            AuthorityPathSuffix = $"/application/o/{appSlug}/"
        };
    }
}
