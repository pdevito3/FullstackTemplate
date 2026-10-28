namespace FullstackTemplate.Server.Resources.Entrypoints;

using DPoP;
using Mcp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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
        // Cognito access tokens have no aud claim. They put the app client ID in client_id.
        var audienceClaimType = configuration["Auth:AudienceClaimType"] ?? "aud";
        var validatesAud = audienceClaimType == "aud";

        // init:start - scripts/init.cs deletes the userinfo check of the conformance suite when it renames the app
        const string accessTokenValidationKey = UserinfoAuthenticationHandler.AccessTokenValidationKey;
        if (string.Equals(configuration[accessTokenValidationKey], "Userinfo", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException($"{accessTokenValidationKey}=Userinfo is only for the conformance tests in Development.");
            var clientId = configuration["Auth:ClientId"]
                ?? throw new InvalidOperationException("Auth:ClientId configuration is required.");

            UserinfoAuthenticationHandler.AddHttpClient(services, configuration[UserinfoAuthenticationHandler.PinnedCertificatePathKey]);
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddScheme<UserinfoAuthenticationOptions, UserinfoAuthenticationHandler>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = authAuthority;
                    options.ClientId = clientId;
                    options.NameClaimType = nameClaimType;
                    options.RoleClaimType = roleClaimType;
                });
            services.AddMcpResourceMetadata(configuration);
            services.AddAuthorization();
            return services;
        }
        // init:end

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authAuthority;
                options.Audience = authAudience;

                options.RequireHttpsMetadata = requireHttpsMetadata;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = validatesAud,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = nameClaimType,
                    RoleClaimType = roleClaimType
                };

                if (!validatesAud)
                {
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            if (context.Principal?.FindFirst(audienceClaimType)?.Value != authAudience)
                            {
                                context.Fail($"The token has no '{audienceClaimType}' claim with the value '{authAudience}'.");
                            }
                            return Task.CompletedTask;
                        }
                    };
                }
            });

        // DPoP (RFC 9449): the DPoP scheme, the proof check, and the rejection of a bound token sent as Bearer
        services.AddDPoP(configuration);
        // After AddDPoP, so that its challenge wraps the DPoP challenge and can put the Bearer challenge first
        services.AddMcpResourceMetadata(configuration);

        services.AddAuthorization();

        return services;
    }
}
