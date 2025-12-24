using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace FullstackTemplate.Server;

/// <summary>
/// Extension methods for configuring API authentication.
/// </summary>
public static class AuthExtensions
{
    /// <summary>
    /// Adds JWT Bearer authentication using configuration from environment variables.
    /// Reads Auth:Authority, Auth:Audience, Auth:RequireHttpsMetadata,
    /// Auth:NameClaimType, and Auth:RoleClaimType from configuration.
    /// </summary>
    public static IServiceCollection AddJwtBearerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var authAuthority = configuration["Auth:Authority"]
            ?? throw new InvalidOperationException("Auth:Authority configuration is required.");
        var authAudience = configuration["Auth:Audience"]
            ?? throw new InvalidOperationException("Auth:Audience configuration is required.");

        // These have defaults for backwards compatibility
        var requireHttpsMetadata = configuration.GetValue("Auth:RequireHttpsMetadata", !environment.IsDevelopment());
        var nameClaimType = configuration["Auth:NameClaimType"] ?? "name";
        var roleClaimType = configuration["Auth:RoleClaimType"] ?? "role";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authAuthority;
                options.Audience = authAudience;

                options.RequireHttpsMetadata = requireHttpsMetadata;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = nameClaimType,
                    RoleClaimType = roleClaimType
                };
            });

        services.AddAuthorization();

        return services;
    }
}
