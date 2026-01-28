namespace FullstackTemplate.Server.Resources.Extensions;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerUI;

/// <summary>
/// Extension methods for configuring Swagger/OpenAPI documentation with OAuth2 support.
/// </summary>
public static class SwaggerExtension
{
    private const string SecuritySchemeName = "oauth2";
    private const string ApiVersion = "1";

    /// <summary>
    /// Adds OpenAPI document generation with optional OAuth2 security and XML documentation.
    /// Uses OIDC discovery to automatically determine authorization and token endpoints.
    /// </summary>
    public static IServiceCollection AddSwaggerExtension(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authAuthority = configuration["Auth:Authority"];
        var authAudience = configuration["Auth:Audience"];
        var useAudienceAsScope = configuration.GetValue("Auth:UseAudienceAsScope", true);
        var hasOAuthConfig = !string.IsNullOrEmpty(authAuthority);

        var scopesConfig = configuration.GetSection("Auth:Scopes").Get<Dictionary<string, string>>();
        if (scopesConfig == null || scopesConfig.Count == 0)
        {
            scopesConfig = new Dictionary<string, string>
            {
                { "openid", "OpenID Connect scope" },
                { "profile", "User profile information" }
            };

            // Some OAuth providers (like Keycloak) require the audience as a scope to get API access tokens
            // Others (like FusionAuth) don't support custom scopes
            if (useAudienceAsScope && !string.IsNullOrEmpty(authAudience))
            {
                scopesConfig[authAudience] = "API access scope";
            }
        }

        // Discover OAuth endpoints from OIDC configuration
        OidcEndpoints? oidcEndpoints = null;
        if (hasOAuthConfig)
        {
            oidcEndpoints = DiscoverOidcEndpoints(authAuthority!);
        }

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "FullstackTemplate API",
                    Version = "v1",
                    Description = "API documentation for the FullstackTemplate application"
                };

                if (hasOAuthConfig && oidcEndpoints != null)
                {
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

                    document.Components.SecuritySchemes[SecuritySchemeName] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.OAuth2,
                        Description = "OAuth2 Authorization Code flow with PKCE",
                        Flows = new OpenApiOAuthFlows
                        {
                            AuthorizationCode = new OpenApiOAuthFlow
                            {
                                AuthorizationUrl = new Uri(oidcEndpoints.AuthorizationEndpoint),
                                TokenUrl = new Uri(oidcEndpoints.TokenEndpoint),
                                Scopes = scopesConfig
                            }
                        }
                    };

                    document.Security =
                    [
                        new OpenApiSecurityRequirement
                        {
                            {
                                new OpenApiSecuritySchemeReference(SecuritySchemeName),
                                scopesConfig.Keys.ToList()
                            }
                        }
                    ];
                }

                var updatedPaths = new OpenApiPaths();
                foreach (var path in document.Paths)
                {
                    var newPath = path.Key
                        .Replace("{version}", ApiVersion)
                        .Replace("{v}", ApiVersion);
                    updatedPaths[newPath] = path.Value;
                }
                document.Paths = updatedPaths;

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                operation.Parameters = operation.Parameters?
                    .Where(p => p.Name != "version" && p.Name != "v")
                    .ToList();

                return Task.CompletedTask;
            });
        });

        services.AddSwaggerGen(options =>
        {
            var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }

    /// <summary>
    /// Discovers OIDC endpoints from the provider's .well-known/openid-configuration.
    /// </summary>
    private static OidcEndpoints? DiscoverOidcEndpoints(string authority)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var discoveryUrl = authority.TrimEnd('/') + "/.well-known/openid-configuration";
            var response = httpClient.GetAsync(discoveryUrl).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Warning: Failed to fetch OIDC discovery document from {discoveryUrl}. " +
                    $"Status: {response.StatusCode}. Swagger OAuth may not work correctly.");
                return null;
            }

            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var authEndpoint = root.GetProperty("authorization_endpoint").GetString();
            var tokenEndpoint = root.GetProperty("token_endpoint").GetString();

            if (string.IsNullOrEmpty(authEndpoint) || string.IsNullOrEmpty(tokenEndpoint))
            {
                Console.WriteLine("Warning: OIDC discovery document missing required endpoints.");
                return null;
            }

            return new OidcEndpoints(authEndpoint, tokenEndpoint);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to discover OIDC endpoints: {ex.Message}. " +
                "Swagger OAuth may not work correctly.");
            return null;
        }
    }

    private sealed record OidcEndpoints(string AuthorizationEndpoint, string TokenEndpoint);

    /// <summary>
    /// Configures and enables Swagger UI middleware with optional OAuth2 authentication.
    /// </summary>
    public static IApplicationBuilder UseSwaggerExtension(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        var authAuthority = configuration["Auth:Authority"];
        var authAudience = configuration["Auth:Audience"];
        var authClientId = configuration["Auth:ClientId"];
        var authClientSecret = configuration["Auth:ClientSecret"];
        var useAudienceAsScope = configuration.GetValue("Auth:UseAudienceAsScope", true);
        var hasOAuthConfig = !string.IsNullOrEmpty(authAuthority);
        var swaggerClientId = configuration["Swagger:ClientId"] ?? authClientId;
        var swaggerClientSecret = configuration["Swagger:ClientSecret"] ?? authClientSecret;

        app.UseSwagger();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "FullstackTemplate API v1");
            options.RoutePrefix = "swagger";

            if (hasOAuthConfig && !string.IsNullOrEmpty(swaggerClientId))
            {
                options.OAuthClientId(swaggerClientId);
                options.OAuthUsePkce();

                if (!string.IsNullOrEmpty(swaggerClientSecret))
                {
                    options.OAuthClientSecret(swaggerClientSecret);
                }

                var scopesToSelect = new List<string> { "openid", "profile" };
                if (useAudienceAsScope && !string.IsNullOrEmpty(authAudience))
                {
                    scopesToSelect.Add(authAudience);
                }
                options.OAuthScopes(scopesToSelect.ToArray());
            }

            options.DocExpansion(DocExpansion.None);
            options.DefaultModelsExpandDepth(-1);
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnableFilter();
            options.ShowExtensions();
            options.EnableTryItOutByDefault();
        });

        return app;
    }
}
