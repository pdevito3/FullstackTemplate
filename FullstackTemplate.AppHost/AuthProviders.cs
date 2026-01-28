using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace FullstackTemplate.AppHost;

/// <summary>
/// Configuration for an authentication provider.
/// </summary>
public sealed record AuthProviderConfig
{
    public required string ProviderName { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string Audience { get; init; }
    public required bool RequireHttpsMetadata { get; init; }
    public required string NameClaimType { get; init; }
    public required string RoleClaimType { get; init; }

    /// <summary>
    /// Static authority URL for external providers (e.g., Duende Demo).
    /// </summary>
    public string? Authority { get; init; }

    /// <summary>
    /// Container resource for local providers (e.g., FusionAuth).
    /// When set, the Authority will be derived from this container's endpoint.
    /// </summary>
    public IResourceBuilder<ContainerResource>? AuthResource { get; init; }

    /// <summary>
    /// Whether to revoke refresh tokens on logout.
    /// Some providers (like FusionAuth) don't expose a revocation endpoint in OIDC discovery.
    /// Default is true for providers that support it.
    /// </summary>
    public bool RevokeRefreshTokenOnLogout { get; init; } = true;

    /// <summary>
    /// Whether to include the Audience as an OAuth scope.
    /// Some providers (like Keycloak) require the audience as a scope to get proper access tokens.
    /// FusionAuth doesn't support custom scopes and will reject unknown scopes.
    /// Default is true for compatibility with most providers.
    /// </summary>
    public bool UseAudienceAsScope { get; init; } = true;

    /// <summary>
    /// Path suffix to append to the container endpoint for the authority URL.
    /// Used by providers like Keycloak that include the realm in the authority path.
    /// Example: "/realms/aspire" for Keycloak.
    /// </summary>
    public string? AuthorityPathSuffix { get; init; }

    /// <summary>
    /// Returns true if this is a container-based provider (like FusionAuth).
    /// </summary>
    public bool IsContainerBased => AuthResource is not null;
}

/// <summary>
/// Extension methods for configuring resources with auth provider settings.
/// </summary>
public static class AuthProviderExtensions
{
    /// <summary>
    /// Applies server authentication environment variables to a resource.
    /// </summary>
    public static IResourceBuilder<T> WithServerAuth<T>(
        this IResourceBuilder<T> resource,
        AuthProviderConfig authProvider) where T : IResourceWithEnvironment
    {
        resource
            .WithEnvironment("Auth__ClientId", authProvider.ClientId)
            .WithEnvironment("Auth__ClientSecret", authProvider.ClientSecret)
            .WithEnvironment("Auth__Audience", authProvider.Audience)
            .WithEnvironment("Auth__RequireHttpsMetadata", authProvider.RequireHttpsMetadata.ToString())
            .WithEnvironment("Auth__NameClaimType", authProvider.NameClaimType)
            .WithEnvironment("Auth__RoleClaimType", authProvider.RoleClaimType)
            .WithEnvironment("Auth__UseAudienceAsScope", authProvider.UseAudienceAsScope.ToString());

        // Apply authority - either static URL or container endpoint
        if (authProvider.Authority is not null)
        {
            resource.WithEnvironment("Auth__Authority", authProvider.Authority);
        }
        else if (authProvider.AuthResource is not null)
        {
            if (authProvider.AuthorityPathSuffix is not null)
            {
                // Build authority URL with path suffix (e.g., for Keycloak realms)
                resource.WithEnvironment(context =>
                {
                    var endpoint = authProvider.AuthResource.GetEndpoint("http");
                    context.EnvironmentVariables["Auth__Authority"] = ReferenceExpression.Create(
                        $"{endpoint}{authProvider.AuthorityPathSuffix}");
                });
            }
            else
            {
                resource.WithEnvironment("Auth__Authority", authProvider.AuthResource.GetEndpoint("http"));
            }
        }

        return resource;
    }

    /// <summary>
    /// Applies BFF authentication environment variables to a resource.
    /// </summary>
    public static IResourceBuilder<T> WithBffAuth<T>(
        this IResourceBuilder<T> resource,
        AuthProviderConfig authProvider) where T : IResourceWithEnvironment
    {
        resource
            .WithEnvironment("Auth__ClientId", authProvider.ClientId)
            .WithEnvironment("Auth__ClientSecret", authProvider.ClientSecret)
            .WithEnvironment("Auth__Audience", authProvider.Audience)
            .WithEnvironment("Auth__RequireHttpsMetadata", authProvider.RequireHttpsMetadata.ToString())
            .WithEnvironment("Auth__NameClaimType", authProvider.NameClaimType)
            .WithEnvironment("Auth__RoleClaimType", authProvider.RoleClaimType)
            .WithEnvironment("Auth__RevokeRefreshTokenOnLogout", authProvider.RevokeRefreshTokenOnLogout.ToString())
            .WithEnvironment("Auth__UseAudienceAsScope", authProvider.UseAudienceAsScope.ToString());

        // Apply authority - either static URL or container endpoint
        if (authProvider.Authority is not null)
        {
            resource.WithEnvironment("Auth__Authority", authProvider.Authority);
        }
        else if (authProvider.AuthResource is not null)
        {
            if (authProvider.AuthorityPathSuffix is not null)
            {
                // Build authority URL with path suffix (e.g., for Keycloak realms)
                resource.WithEnvironment(context =>
                {
                    var endpoint = authProvider.AuthResource.GetEndpoint("http");
                    context.EnvironmentVariables["Auth__Authority"] = ReferenceExpression.Create(
                        $"{endpoint}{authProvider.AuthorityPathSuffix}");
                });
            }
            else
            {
                resource.WithEnvironment("Auth__Authority", authProvider.AuthResource.GetEndpoint("http"));
            }
        }

        return resource;
    }
}

/// <summary>
/// Factory methods for creating authentication provider configurations.
/// </summary>
public static class AuthProviders
{
//#if (UseDuendeDemo)
    /// <summary>
    /// Creates a configuration for the Duende Demo identity server.
    /// This is a public demo server useful for testing.
    /// </summary>
    public static AuthProviderConfig DuendeDemo() => new()
    {
        ProviderName = "DuendeDemo",
        Authority = "https://demo.duendesoftware.com",
        ClientId = "interactive.confidential",
        ClientSecret = "secret",
        Audience = "api",
        RequireHttpsMetadata = true,
        NameClaimType = "name",
        RoleClaimType = "role",
        AuthResource = null
    };
//#endif

//#if (UseFusionAuth)
    /// <summary>
    /// Creates a configuration for FusionAuth running locally in Docker.
    /// Sets up PostgreSQL database and FusionAuth container with Kickstart auto-provisioning.
    /// </summary>
    public static AuthProviderConfig FusionAuth(IDistributedApplicationBuilder builder)
    {
        // PostgreSQL database for FusionAuth
        var fusionAuthDbPassword = builder.AddParameter("fusionauth-db-password", secret: true);
        var fusionAuthPostgres = builder.AddPostgres("fusionauth-postgres", password: fusionAuthDbPassword)
            .WithDataVolume("fusionauth-postgres-data");
        var fusionAuthDb = fusionAuthPostgres.AddDatabase("fusionauth-db");

        // Get the postgres endpoint for container-to-container communication
        var postgresEndpoint = fusionAuthPostgres.GetEndpoint("tcp", KnownNetworkIdentifiers.DefaultAspireContainerNetwork);

        // FusionAuth Identity Provider container
        var fusionAuth = builder.AddContainer("fusionauth", "fusionauth/fusionauth-app", "latest")
            .WithHttpEndpoint(port: 9011, targetPort: 9011, name: "http")
            .WithReference(fusionAuthDb)
            .WithEnvironment(context =>
            {
                // Build JDBC connection string for FusionAuth
                // Use the container network endpoint for proper DNS resolution
                context.EnvironmentVariables["DATABASE_URL"] = ReferenceExpression.Create(
                    $"jdbc:postgresql://{postgresEndpoint.Property(EndpointProperty.Host)}:{postgresEndpoint.Property(EndpointProperty.Port)}/fusionauth-db");
                context.EnvironmentVariables["DATABASE_USERNAME"] = "postgres";
                context.EnvironmentVariables["DATABASE_PASSWORD"] = fusionAuthDbPassword;
            })
            .WithEnvironment("FUSIONAUTH_APP_MEMORY", "512M")
            .WithEnvironment("FUSIONAUTH_APP_RUNTIME_MODE", "development")
            .WithEnvironment("FUSIONAUTH_APP_URL", "http://localhost:9011")
            .WithEnvironment("FUSIONAUTH_APP_KICKSTART_FILE", "/usr/local/fusionauth/kickstart/kickstart.json")
            .WithEnvironment("FUSIONAUTH_APP_SILENT_MODE", "true")
            .WithEnvironment("SEARCH_TYPE", "database")
            .WithBindMount("./fusionauth", "/usr/local/fusionauth/kickstart", isReadOnly: true)
            .WaitFor(fusionAuthDb)
            // Health check ensures FusionAuth is ready before server starts
            .WithHttpHealthCheck("/api/status");

        return new AuthProviderConfig
        {
            ProviderName = "FusionAuth",
            ClientId = "e9fdb985-9173-4e01-9d73-ac2d60d1dc8e",
            ClientSecret = "super-secret-client-secret-change-in-production",
            Audience = "e9fdb985-9173-4e01-9d73-ac2d60d1dc8e",
            RequireHttpsMetadata = false,
            NameClaimType = "email",
            RoleClaimType = "roles",
            RevokeRefreshTokenOnLogout = false, // FusionAuth doesn't expose revocation in OIDC discovery
            UseAudienceAsScope = false, // FusionAuth doesn't support custom scopes
            AuthResource = fusionAuth
        };
    }
//#endif

//#if (UseKeycloak)
    /// <summary>
    /// Creates a configuration for Keycloak running locally in Docker.
    /// Uses PostgreSQL database with data volume for persistence and realm import for auto-provisioning.
    /// </summary>
    public static AuthProviderConfig Keycloak(IDistributedApplicationBuilder builder)
    {
        const string realmName = "aspire";
        const string clientId = "aspire-app";

        // PostgreSQL database for Keycloak
        var keycloakDbPassword = builder.AddParameter("keycloak-db-password", secret: true);
        var keycloakPostgres = builder.AddPostgres("keycloak-postgres", password: keycloakDbPassword)
            .WithDataVolume("keycloak-postgres-data");
        var keycloakDb = keycloakPostgres.AddDatabase("keycloak-db");

        // Get the postgres endpoint for container-to-container communication
        var postgresEndpoint = keycloakPostgres.GetEndpoint("tcp", KnownNetworkIdentifiers.DefaultAspireContainerNetwork);

        // Keycloak Identity Provider container with PostgreSQL
        var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.0")
            .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
            .WithReference(keycloakDb)
            .WithEnvironment("KEYCLOAK_ADMIN", "admin")
            .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
            .WithEnvironment("KC_HEALTH_ENABLED", "true")
            .WithEnvironment("KC_HTTP_ENABLED", "true")
            .WithEnvironment("KC_DB", "postgres")
            .WithEnvironment("KC_DB_USERNAME", "postgres")
            .WithEnvironment("KC_DB_PASSWORD", keycloakDbPassword)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["KC_DB_URL_HOST"] = postgresEndpoint.Property(EndpointProperty.Host);
                context.EnvironmentVariables["KC_DB_URL_PORT"] = postgresEndpoint.Property(EndpointProperty.Port);
            })
            .WithEnvironment("KC_DB_URL_DATABASE", "keycloak-db")
            .WithBindMount("./keycloak", "/opt/keycloak/data/import", isReadOnly: true)
            .WithArgs("start-dev", "--import-realm")
            .WaitFor(keycloakDb)
            // Use realm endpoint for health check since management port isn't exposed in dev mode
            .WithHttpHealthCheck($"/realms/{realmName}");

        // Group Keycloak resources under the main container in the dashboard
        keycloakPostgres.WithParentRelationship(keycloak);

        return new AuthProviderConfig
        {
            ProviderName = "Keycloak",
            ClientId = clientId,
            ClientSecret = "super-secret-client-secret-change-in-production",
            Audience = clientId,
            RequireHttpsMetadata = false,
            NameClaimType = "email",
            RoleClaimType = "roles",
            RevokeRefreshTokenOnLogout = true,
            AuthResource = keycloak,
            AuthorityPathSuffix = $"/realms/{realmName}"
        };
    }
//#endif

//#if (UseAuthentik)
    /// <summary>
    /// Creates a configuration for Authentik running locally in Docker.
    /// Sets up PostgreSQL, Redis, server, and worker containers with blueprint auto-provisioning.
    /// </summary>
    public static AuthProviderConfig Authentik(IDistributedApplicationBuilder builder)
    {
        const string appSlug = "aspire-app";
        const string clientId = "aspire-app";

        var authentikSecretKey = builder.AddParameter("authentik-secret-key", secret: true);

        // PostgreSQL database for Authentik
        var authentikDbPassword = builder.AddParameter("authentik-db-password", secret: true);
        var authentikPostgres = builder.AddPostgres("authentik-postgres", password: authentikDbPassword)
            .WithDataVolume("authentik-postgres-data");
        var authentikDb = authentikPostgres.AddDatabase("authentik-db");

        var postgresEndpoint = authentikPostgres.GetEndpoint("tcp", KnownNetworkIdentifiers.DefaultAspireContainerNetwork);

        // Redis for Authentik with data persistence
        var redis = builder.AddContainer("authentik-redis", "redis", "alpine")
            .WithArgs("--save", "60", "1", "--loglevel", "warning")
            .WithVolume("authentik-redis-data", "/data");

        var redisEndpoint = redis.GetEndpoint("tcp", KnownNetworkIdentifiers.DefaultAspireContainerNetwork);

        // Authentik Worker (must start before server to apply blueprints)
        var worker = builder.AddContainer("authentik-worker", "ghcr.io/goauthentik/server", "2024.12")
            .WithReference(authentikDb)
            .WithEnvironment("AUTHENTIK_SECRET_KEY", authentikSecretKey)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["AUTHENTIK_POSTGRESQL__HOST"] = postgresEndpoint.Property(EndpointProperty.Host);
                context.EnvironmentVariables["AUTHENTIK_POSTGRESQL__PORT"] = postgresEndpoint.Property(EndpointProperty.Port);
                context.EnvironmentVariables["AUTHENTIK_REDIS__HOST"] = redisEndpoint.Property(EndpointProperty.Host);
            })
            .WithEnvironment("AUTHENTIK_POSTGRESQL__USER", "postgres")
            .WithEnvironment("AUTHENTIK_POSTGRESQL__NAME", "authentik-db")
            .WithEnvironment("AUTHENTIK_POSTGRESQL__PASSWORD", authentikDbPassword)
            .WithEnvironment("AUTHENTIK_BOOTSTRAP_PASSWORD", "password123!")
            .WithEnvironment("AUTHENTIK_BOOTSTRAP_EMAIL", "akadmin@example.com")
            .WithBindMount("./authentik", "/blueprints/custom", isReadOnly: true)
            .WithArgs("worker")
            .WaitFor(authentikDb)
            .WaitFor(redis);

        // Authentik Server (parent resource for dashboard grouping)
        var server = builder.AddContainer("authentik", "ghcr.io/goauthentik/server", "2024.12")
            .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "http")
            .WithReference(authentikDb)
            .WithEnvironment("AUTHENTIK_SECRET_KEY", authentikSecretKey)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["AUTHENTIK_POSTGRESQL__HOST"] = postgresEndpoint.Property(EndpointProperty.Host);
                context.EnvironmentVariables["AUTHENTIK_POSTGRESQL__PORT"] = postgresEndpoint.Property(EndpointProperty.Port);
                context.EnvironmentVariables["AUTHENTIK_REDIS__HOST"] = redisEndpoint.Property(EndpointProperty.Host);
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
        redis.WithParentRelationship(server);
        worker.WithParentRelationship(server);

        return new AuthProviderConfig
        {
            ProviderName = "Authentik",
            ClientId = clientId,
            ClientSecret = "super-secret-client-secret-change-in-production",
            Audience = clientId,
            RequireHttpsMetadata = false,
            NameClaimType = "email",
            RoleClaimType = "roles",
            RevokeRefreshTokenOnLogout = true,
            AuthResource = server,
            AuthorityPathSuffix = $"/application/o/{appSlug}/"
        };
    }
//#endif
}
