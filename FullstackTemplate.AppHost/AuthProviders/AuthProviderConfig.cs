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
    /// The Auth:Features settings of the BFF. The defaults decide each feature from the discovery document of the
    /// provider, which is correct for each provider.
    /// </summary>
    public AuthFeatureSettings Features { get; init; } = new();

    /// <summary>
    /// The DPoP key of the BFF (<c>Auth:DPoPJsonWebKey</c>), when the stack runs with DPoP. See
    /// <see cref="AuthProviderDPoP.WithDPoP"/>.
    /// </summary>
    public IResourceBuilder<ParameterResource>? DPoPKey { get; init; }

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

    /// <summary>
    /// More environment variables for the BFF only, for a provider that needs a BFF setting that no other provider needs.
    /// </summary>
    public IReadOnlyDictionary<string, string> BffSettings { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// More environment variables for the server only, for a provider that needs a server setting that no other
    /// provider needs.
    /// </summary>
    public IReadOnlyDictionary<string, string> ServerSettings { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Changes the BFF resource after the AppHost has given it the auth settings, for a provider that must change how
    /// the BFF starts or listens.
    /// </summary>
    public Action<IResourceBuilder<ProjectResource>>? ConfigureBff { get; init; }
}

/// <summary>
/// The Auth:Features settings of the BFF: Auto, Require, or Disable for each optional feature of the provider.
/// See section 3.3 of docs/specs/home-rolled-bff.md.
/// </summary>
public sealed record AuthFeatureSettings
{
    public string Revocation { get; init; } = "Auto";
    public string IssuerParameter { get; init; } = "Auto";
    public string BackChannelLogout { get; init; } = "Auto";
    public string EndSession { get; init; } = "Auto";
    public string PushedAuthorization { get; init; } = "Auto";
    public string DPoP { get; init; } = "Disable";
}

/// <summary>
/// The app URLs that a local identity provider must accept as redirect targets.
/// An isolated run gives these endpoints random ports, so the seed files read them from environment variables.
/// The BFF builds its OIDC redirect URI from its own URL, because the Vite proxy changes the origin.
/// <para><paramref name="BffForContainers"/> is the BFF http endpoint in the container network view. The identity
/// provider containers send the back-channel logout token to it. The http endpoint, because the containers do not
/// trust the development certificate.</para>
/// </summary>
public sealed record AuthClientUrls(
    EndpointReference Frontend,
    EndpointReference Server,
    EndpointReference Bff,
    EndpointReference BffForContainers)
{
    public const string FrontendVariable = "APP_FRONTEND_URL";
    public const string ServerVariable = "APP_SERVER_URL";
    public const string BffVariable = "APP_BFF_URL";
    public const string BffBackChannelVariable = "APP_BFF_BACKCHANNEL_URL";
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

        foreach (var (name, value) in authProvider.ServerSettings)
        {
            resource.WithEnvironment(name, value);
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
            context.EnvironmentVariables[AuthClientUrls.BffBackChannelVariable] =
                ReferenceExpression.Create($"{clientUrls.BffForContainers}/bff/backchannel");
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
            .WithEnvironment("Auth__UseAudienceAsScope", authProvider.UseAudienceAsScope.ToString())
            .WithEnvironment("Auth__RequestOfflineAccess", authProvider.RequestOfflineAccess.ToString())
            .WithEnvironment("Auth__Features__Revocation", authProvider.Features.Revocation)
            .WithEnvironment("Auth__Features__IssuerParameter", authProvider.Features.IssuerParameter)
            .WithEnvironment("Auth__Features__BackChannelLogout", authProvider.Features.BackChannelLogout)
            .WithEnvironment("Auth__Features__EndSession", authProvider.Features.EndSession)
            .WithEnvironment("Auth__Features__PushedAuthorization", authProvider.Features.PushedAuthorization)
            .WithEnvironment("Auth__Features__DPoP", authProvider.Features.DPoP);

        if (authProvider.DPoPKey is not null)
        {
            resource.WithEnvironment("Auth__DPoPJsonWebKey", authProvider.DPoPKey);
        }

        foreach (var (name, value) in authProvider.BffSettings)
        {
            resource.WithEnvironment(name, value);
        }

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
