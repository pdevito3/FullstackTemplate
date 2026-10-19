using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace FullstackTemplate.AppHost;

/// <summary>
/// Configuration for an authentication provider.
/// </summary>
public sealed record AuthProviderConfig
{
    public required string ProviderName { get; init; }
    public required string Authority { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string Audience { get; init; }
    public required bool RequireHttpsMetadata { get; init; }
    public required string NameClaimType { get; init; }
    public required string RoleClaimType { get; init; }

    /// <summary>
    /// Whether to revoke refresh tokens on logout.
    /// Default is true for providers that support it.
    /// </summary>
    public bool RevokeRefreshTokenOnLogout { get; init; } = true;

    /// <summary>
    /// Whether to include the Audience as an OAuth scope.
    /// Default is true for compatibility with most providers.
    /// </summary>
    public bool UseAudienceAsScope { get; init; } = true;
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
        return resource
            .WithEnvironment("Auth__Authority", authProvider.Authority)
            .WithEnvironment("Auth__ClientId", authProvider.ClientId)
            .WithEnvironment("Auth__ClientSecret", authProvider.ClientSecret)
            .WithEnvironment("Auth__Audience", authProvider.Audience)
            .WithEnvironment("Auth__RequireHttpsMetadata", authProvider.RequireHttpsMetadata.ToString())
            .WithEnvironment("Auth__NameClaimType", authProvider.NameClaimType)
            .WithEnvironment("Auth__RoleClaimType", authProvider.RoleClaimType)
            .WithEnvironment("Auth__UseAudienceAsScope", authProvider.UseAudienceAsScope.ToString());
    }

    /// <summary>
    /// Applies BFF authentication environment variables to a resource.
    /// </summary>
    public static IResourceBuilder<T> WithBffAuth<T>(
        this IResourceBuilder<T> resource,
        AuthProviderConfig authProvider) where T : IResourceWithEnvironment
    {
        return resource
            .WithEnvironment("Auth__Authority", authProvider.Authority)
            .WithEnvironment("Auth__ClientId", authProvider.ClientId)
            .WithEnvironment("Auth__ClientSecret", authProvider.ClientSecret)
            .WithEnvironment("Auth__Audience", authProvider.Audience)
            .WithEnvironment("Auth__RequireHttpsMetadata", authProvider.RequireHttpsMetadata.ToString())
            .WithEnvironment("Auth__NameClaimType", authProvider.NameClaimType)
            .WithEnvironment("Auth__RoleClaimType", authProvider.RoleClaimType)
            .WithEnvironment("Auth__RevokeRefreshTokenOnLogout", authProvider.RevokeRefreshTokenOnLogout.ToString())
            .WithEnvironment("Auth__UseAudienceAsScope", authProvider.UseAudienceAsScope.ToString());
    }
}

/// <summary>
/// Factory methods for creating authentication provider configurations.
/// </summary>
public static class AuthProviders
{
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
        RoleClaimType = "role"
    };
}
