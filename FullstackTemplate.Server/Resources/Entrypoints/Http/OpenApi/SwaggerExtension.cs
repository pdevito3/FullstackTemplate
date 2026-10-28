namespace FullstackTemplate.Server.Resources.Entrypoints.Http.OpenApi;

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

    /// <summary>
    /// Adds OpenAPI document generation with optional OAuth2 security and XML documentation.
    /// Uses OIDC discovery to automatically determine authorization and token endpoints.
    /// </summary>
    public static IServiceCollection AddSwaggerExtension(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authAuthority = configuration["Auth:Authority"];
        var hasOAuthConfig = !string.IsNullOrEmpty(authAuthority);
        var scopesConfig = ApiScopes.FromConfiguration(configuration);

        // Discover OAuth endpoints from OIDC configuration.
        // No auth provider runs during the build-time document generation, so it skips discovery.
        OidcEndpoints? oidcEndpoints = null;
        if (hasOAuthConfig && !OpenApiBuildTime.IsActive)
        {
            oidcEndpoints = DiscoverOidcEndpoints(authAuthority!);
        }

        // Each supported API version gets its own document, named by its ApiExplorer group ("v1", "v2")
        foreach (var version in ApiVersioningExtension.SupportedVersions)
            services.AddOpenApi(ApiVersioningExtension.DocumentName(version), ConfigureDocument);

        return services;

        void ConfigureDocument(OpenApiOptions options)
        {
            // ProducesValidationProblem declares HttpValidationProblemDetails. The contract keeps the name
            // ValidationProblemDetails, which it had with MVC. The two types have the same JSON shape.
            options.CreateSchemaReferenceId = type => type.Type == typeof(HttpValidationProblemDetails)
                ? "ValidationProblemDetails"
                : OpenApiOptions.CreateDefaultSchemaReferenceId(type);

            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "FullstackTemplate API",
                    Version = context.DocumentName,
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
                                new OpenApiSecuritySchemeReference(SecuritySchemeName, document),
                                scopesConfig.Keys.ToList()
                            }
                        }
                    ];
                }

                return Task.CompletedTask;
            });

            options.AddSchemaTransformer<RequireNonNullablePropertiesTransformer>();
            options.AddSchemaTransformer<RemoveStringFromNumbersTransformer>();
            options.AddSchemaTransformer<UnsignedIntegerRangeTransformer>();
            options.AddSchemaTransformer<FixedNamesTransformer>();
            options.AddSchemaTransformer<SchemaLimitsTransformer>();
            options.AddOperationTransformer<PaginationHeaderTransformer>();
            options.AddOperationTransformer<JsonContentTypeTransformer>();
            options.AddOperationTransformer<UnauthorizedResponseTransformer>();
            options.AddDocumentTransformer<RequireOperationIdTransformer>();
            options.AddDocumentTransformer<RequireListOperationNameTransformer>();
        }
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

        app.UseSwaggerUI(options =>
        {
            foreach (var version in ApiVersioningExtension.SupportedVersions)
            {
                var documentName = ApiVersioningExtension.DocumentName(version);
                options.SwaggerEndpoint($"/openapi/{documentName}.json", $"FullstackTemplate API {documentName}");
            }
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
