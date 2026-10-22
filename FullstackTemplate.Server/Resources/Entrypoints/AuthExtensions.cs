namespace FullstackTemplate.Server.Resources.Entrypoints;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Extension methods for configuring API authentication.
/// </summary>
public static class AuthExtensions
{
    /// <summary>
    /// <c>Jwt</c> (the default) or <c>Userinfo</c>. With <c>Userinfo</c>, the userinfo endpoint of the identity
    /// provider checks each access token, see <see cref="UserinfoAuthenticationHandler"/>. Only the OpenID
    /// conformance suite provider of the AppHost sets it, because the suite issues opaque access tokens. Development
    /// only.
    /// </summary>
    public const string AccessTokenValidationKey = "Auth:AccessTokenValidation";

    /// <summary>
    /// A PEM file with the one certificate that the userinfo check accepts from the identity provider, such as the
    /// self-signed certificate of the conformance suite.
    /// </summary>
    public const string PinnedCertificatePathKey = "Auth:PinnedCertificatePath";

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

        if (string.Equals(configuration[AccessTokenValidationKey], "Userinfo", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException($"{AccessTokenValidationKey}=Userinfo is only for the conformance tests in Development.");

            UserinfoAuthenticationHandler.AddHttpClient(services, configuration[PinnedCertificatePathKey]);
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddScheme<UserinfoAuthenticationOptions, UserinfoAuthenticationHandler>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = authAuthority;
                    options.NameClaimType = nameClaimType;
                    options.RoleClaimType = roleClaimType;
                });
            services.AddAuthorization();
            return services;
        }

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

        services.AddAuthorization();

        return services;
    }
}
