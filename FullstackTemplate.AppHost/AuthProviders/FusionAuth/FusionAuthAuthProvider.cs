using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FullstackTemplate.AppHost.AuthProviders;

/// <summary>
/// Creates a configuration for FusionAuth running locally in Docker.
/// Sets up PostgreSQL database and FusionAuth container with Kickstart auto-provisioning.
/// Test users: admin@example.com (roles admin and user) and user@example.com (role user). The password is password123!.
/// </summary>
public sealed class FusionAuthAuthProvider : IAuthProvider
{
    private const string KickstartHealthCheck = "fusionauth-kickstart";

    // The values of the variables apiKey and testUserEmail in seed/kickstart.json.
    private const string KickstartApiKey = "super-secret-api-key-for-development-only";
    private const string KickstartTestUserEmail = "user@example.com";
    // The ENV variable of seed/kickstart.json for the issuer of the tenant
    private const string UrlVariable = "APP_FUSIONAUTH_URL";

    private static readonly HttpClient HealthClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public AuthProviderConfig AddTo(IDistributedApplicationBuilder builder, AuthClientUrls clientUrls)
    {
        // PostgreSQL database for FusionAuth
        var fusionAuthDbPassword = builder.AddParameter("fusionauth-db-password", new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true, persist: true);
        var fusionAuthPostgres = builder.AddPostgres("fusionauth-postgres", password: fusionAuthDbPassword)
            .WithImageTag(Config.PostgresImageTag)
            .WithDataVolumeUnlessIsolated();
        var fusionAuthDb = fusionAuthPostgres.AddDatabase("fusionauth-db");

        // Get the postgres endpoint for container-to-container communication
        var postgresEndpoint = fusionAuthPostgres.GetEndpoint("tcp", KnownNetworkIdentifiers.DefaultAspireContainerNetwork);

        // FusionAuth Identity Provider container
        var fusionAuth = builder.AddContainer("fusionauth", "fusionauth/fusionauth-app", "1.69.4")
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
            // Kickstart sends all its requests on one keep-alive connection to the local port 9012. After a response,
            // that port closes a connection whose read rate over its life is below 16 KiB/s, and Kickstart does not
            // retry. Thus the test user can stay missing and the health check never passes (issue #76). A new
            // connection for each request gets 5 s before the check applies, and Kickstart sends a request in less.
            .WithEnvironment("FUSIONAUTH_APP_ADDITIONAL_JAVA_ARGS", "-Dhttp.keepAlive=false")
            .WithEnvironment("SEARCH_TYPE", "database")
            .WithSeedFiles(this, "/usr/local/fusionauth/kickstart")
            .WithClientUrls(clientUrls)
            .WaitFor(fusionAuthDb)
            .WithHealthCheck(KickstartHealthCheck);

        // The BFF reads the discovery document and the signing keys at startup. /api/status is healthy before
        // Kickstart has run, so the health check waits for the test user, the last item that Kickstart creates.
        var fusionAuthHttp = fusionAuth.GetEndpoint("http");
        // Kickstart sets the issuer of the tenant to the URL that the BFF and the Server use as the authority (the
        // host view). OIDC Discovery 4.3 requires them to be identical, and the BFF checks it at startup.
        fusionAuth.WithEnvironment(context => context.EnvironmentVariables[UrlVariable] = fusionAuthHttp.Url);
        builder.Services.AddHealthChecks().AddAsyncCheck(KickstartHealthCheck, async cancellationToken =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{fusionAuthHttp.Url}/api/user?email={Uri.EscapeDataString(KickstartTestUserEmail)}");
            request.Headers.TryAddWithoutValidation("Authorization", KickstartApiKey);
            try
            {
                using var response = await HealthClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return HealthCheckResult.Unhealthy($"Kickstart has not created the test user yet ({(int)response.StatusCode}).");

                // FusionAuth can serve the old issuer from its tenant cache for a few seconds after Kickstart
                // changes it, so wait until the discovery document has the new issuer.
                using var discovery = JsonDocument.Parse(await HealthClient.GetStringAsync(
                    $"{fusionAuthHttp.Url}/.well-known/openid-configuration", cancellationToken));
                var issuer = discovery.RootElement.TryGetProperty("issuer", out var value) ? value.GetString() : null;
                return string.Equals(issuer?.TrimEnd('/'), fusionAuthHttp.Url.TrimEnd('/'), StringComparison.Ordinal)
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy($"The discovery document still has the issuer {issuer}.");
            }
            catch (HttpRequestException exception)
            {
                return HealthCheckResult.Unhealthy("FusionAuth does not answer yet.", exception);
            }
        });

        return new AuthProviderConfig
        {
            ClientId = "e9fdb985-9173-4e01-9d73-ac2d60d1dc8e",
            ClientSecret = "super-secret-client-secret-change-in-production",
            Audience = "e9fdb985-9173-4e01-9d73-ac2d60d1dc8e",
            RequireHttpsMetadata = false,
            NameClaimType = "email",
            RoleClaimType = "roles",
            UseAudienceAsScope = false, // FusionAuth doesn't support custom scopes
            AuthResource = fusionAuth
        };
    }
}
