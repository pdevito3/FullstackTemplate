using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Yarp;

namespace FullstackTemplate.Bff;

/// <summary>
/// Extension methods for configuring BFF authentication.
/// </summary>
public static class AuthExtensions
{
    /// <summary>
    /// Adds BFF authentication using configuration from environment variables.
    /// Reads Auth:Authority, Auth:ClientId, Auth:ClientSecret, Auth:RequireHttpsMetadata,
    /// Auth:NameClaimType, and Auth:RoleClaimType from configuration.
    /// </summary>
    public static IServiceCollection AddBffAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var authAuthority = configuration["Auth:Authority"]
            ?? throw new InvalidOperationException("Auth:Authority configuration is required.");
        var authClientId = configuration["Auth:ClientId"]
            ?? throw new InvalidOperationException("Auth:ClientId configuration is required.");
        var authClientSecret = configuration["Auth:ClientSecret"]
            ?? throw new InvalidOperationException("Auth:ClientSecret configuration is required.");
        var authAudience = configuration["Auth:Audience"];

        var requireHttpsMetadata = configuration.GetValue("Auth:RequireHttpsMetadata", !environment.IsDevelopment());
        var nameClaimType = configuration["Auth:NameClaimType"] ?? "name";
        var roleClaimType = configuration["Auth:RoleClaimType"] ?? "role";
        var revokeRefreshTokenOnLogout = configuration.GetValue("Auth:RevokeRefreshTokenOnLogout", true);
        var refreshBeforeExpirationSeconds = configuration.GetValue("Auth:RefreshBeforeExpirationSeconds", 120);

        services.AddOpenIdConnectAccessTokenManagement(options =>
        {
            options.RefreshBeforeExpiration = TimeSpan.FromSeconds(refreshBeforeExpirationSeconds);
        });

        services.AddBff(options =>
        {
            options.RevokeRefreshTokenOnLogout = revokeRefreshTokenOnLogout;
        })
            .ConfigureOpenIdConnect(options =>
            {
                options.Authority = authAuthority;
                options.ClientId = authClientId;
                options.ClientSecret = authClientSecret;

                options.RequireHttpsMetadata = requireHttpsMetadata;

                options.ResponseType = "code";
                options.ResponseMode = "query";
                options.UsePkce = true;

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;
                options.MapInboundClaims = false;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("offline_access");

                // Request the API audience scope to get access tokens with correct audience claim
                if (!string.IsNullOrEmpty(authAudience))
                {
                    options.Scope.Add(authAudience);
                }

                options.TokenValidationParameters = new()
                {
                    NameClaimType = nameClaimType,
                    RoleClaimType = roleClaimType
                };
            })
            .ConfigureCookies(options =>
            {
                options.Cookie.Name = "__Host-bff";
                // Lax is required for OIDC redirects from external IdP
                options.Cookie.SameSite = SameSiteMode.Lax;
                // Ensure cookie works across ports in dev
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            })
            .AddRemoteApis();

        services.AddAuthorization();

        return services;
    }
}
