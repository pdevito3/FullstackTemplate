using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace FullstackTemplate.AppHost.AuthProviders;

/// <summary>
/// Configuration for an authentication provider.
/// </summary>
public sealed record AuthProviderConfig
{
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
    /// Whether the BFF requests the offline_access scope for a refresh token.
    /// Cognito rejects this scope with invalid_scope and returns a refresh token without it.
    /// </summary>
    public bool RequestOfflineAccess { get; init; } = true;

    /// <summary>
    /// The access token claim that the API compares with the Audience.
    /// Cognito access tokens have no aud claim. They put the app client ID in client_id.
    /// </summary>
    public string AudienceClaimType { get; init; } = "aud";

    /// <summary>
    /// Path of the logout endpoint on the container, for providers whose discovery document has no end_session_endpoint.
    /// The BFF sends client_id and logout_uri to it, as Cognito requires.
    /// </summary>
    public string? LogoutPath { get; init; }
}

/// <summary>
/// The app URLs that a local identity provider must accept as redirect targets.
/// An isolated run gives these endpoints random ports, so the seed files read them from environment variables.
/// The BFF builds its OIDC redirect URI from its own URL, because the Vite proxy changes the origin.
/// </summary>
public sealed record AuthClientUrls(EndpointReference Frontend, EndpointReference Server, EndpointReference Bff)
{
    public const string FrontendVariable = "APP_FRONTEND_URL";
    public const string ServerVariable = "APP_SERVER_URL";
    public const string BffVariable = "APP_BFF_URL";
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
        AuthProviderConfig authProvider) where T : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        resource
            .WithEnvironment("Auth__ClientId", authProvider.ClientId)
            .WithEnvironment("Auth__ClientSecret", authProvider.ClientSecret)
            .WithEnvironment("Auth__Audience", authProvider.Audience)
            .WithEnvironment("Auth__RequireHttpsMetadata", authProvider.RequireHttpsMetadata.ToString())
            .WithEnvironment("Auth__NameClaimType", authProvider.NameClaimType)
            .WithEnvironment("Auth__RoleClaimType", authProvider.RoleClaimType)
            .WithEnvironment("Auth__UseAudienceAsScope", authProvider.UseAudienceAsScope.ToString())
            .WithEnvironment("Auth__AudienceClaimType", authProvider.AudienceClaimType);

        // Apply authority - either static URL or container endpoint
        if (authProvider.Authority is not null)
        {
            resource.WithEnvironment("Auth__Authority", authProvider.Authority);
        }
        else if (authProvider.AuthResource is not null)
        {
            // The app reads the OIDC discovery document at startup, so it waits for the health check of the provider
            resource.WaitFor(authProvider.AuthResource);

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
    /// Gives the app URLs to an identity provider container for its redirect URI seed data.
    /// </summary>
    public static IResourceBuilder<ContainerResource> WithClientUrls(
        this IResourceBuilder<ContainerResource> container,
        AuthClientUrls clientUrls)
    {
        // Url is the host view (http://localhost:<port>) that the browser uses. A plain endpoint reference
        // in a container environment resolves to the container network view instead.
        return container.WithEnvironment(context =>
        {
            context.EnvironmentVariables[AuthClientUrls.FrontendVariable] = clientUrls.Frontend.Url;
            context.EnvironmentVariables[AuthClientUrls.ServerVariable] = clientUrls.Server.Url;
            context.EnvironmentVariables[AuthClientUrls.BffVariable] = clientUrls.Bff.Url;
        });
    }

    /// <summary>
    /// Applies BFF authentication environment variables to a resource.
    /// </summary>
    public static IResourceBuilder<T> WithBffAuth<T>(
        this IResourceBuilder<T> resource,
        AuthProviderConfig authProvider) where T : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        resource
            .WithEnvironment("Auth__ClientId", authProvider.ClientId)
            .WithEnvironment("Auth__ClientSecret", authProvider.ClientSecret)
            .WithEnvironment("Auth__Audience", authProvider.Audience)
            .WithEnvironment("Auth__RequireHttpsMetadata", authProvider.RequireHttpsMetadata.ToString())
            .WithEnvironment("Auth__NameClaimType", authProvider.NameClaimType)
            .WithEnvironment("Auth__RoleClaimType", authProvider.RoleClaimType)
            .WithEnvironment("Auth__RevokeRefreshTokenOnLogout", authProvider.RevokeRefreshTokenOnLogout.ToString())
            .WithEnvironment("Auth__UseAudienceAsScope", authProvider.UseAudienceAsScope.ToString())
            .WithEnvironment("Auth__RequestOfflineAccess", authProvider.RequestOfflineAccess.ToString());

        if (authProvider is { LogoutPath: not null, AuthResource: not null })
        {
            resource.WithEnvironment(context =>
            {
                var endpoint = authProvider.AuthResource.GetEndpoint("http");
                context.EnvironmentVariables["Auth__LogoutEndpoint"] = ReferenceExpression.Create(
                    $"{endpoint}{authProvider.LogoutPath}");
            });
        }

        // Apply authority - either static URL or container endpoint
        if (authProvider.Authority is not null)
        {
            resource.WithEnvironment("Auth__Authority", authProvider.Authority);
        }
        else if (authProvider.AuthResource is not null)
        {
            // The app reads the OIDC discovery document at startup, so it waits for the health check of the provider
            resource.WaitFor(authProvider.AuthResource);

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
