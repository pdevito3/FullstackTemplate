namespace FullstackTemplate.Server.Resources.Entrypoints;

using DPoP;
using Mcp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
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

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.SecurityToken is JsonWebToken token && IsNotAnAccessToken(token))
                            context.Fail("The token is an ID token or a logout token, not an access token.");
                        else if (!validatesAud && context.Principal?.FindFirst(audienceClaimType)?.Value != authAudience)
                            context.Fail($"The token has no '{audienceClaimType}' claim with the value '{authAudience}'.");
                        return Task.CompletedTask;
                    }
                };
            });

        // DPoP (RFC 9449): the DPoP scheme, the proof check, and the rejection of a bound token sent as Bearer
        services.AddDPoP(configuration);
        // After AddDPoP, so that its challenge wraps the DPoP challenge and can put the Bearer challenge first
        services.AddMcpResourceMetadata(configuration);

        services.AddAuthorization();

        return services;
    }

    // An ID token of the BFF has the client ID of the BFF as its aud, and a provider can give the API the same audience.
    // The browser sees the ID token in the id_token_hint of the logout, so an ID token must not act as the user here.
    // The token type has no standard claim, so this reads the claim of each provider that has one. An Authentik ID token
    // has none, but it has no azp either, so CallerIdentityResolver takes it as a token of an unknown client.
    private static bool IsNotAnAccessToken(JsonWebToken token) =>
        // Keycloak: ID, Logout, or Refresh, and Bearer for an access token
        (token.TryGetPayloadValue<string>("typ", out var typ) && typ is "ID" or "Logout" or "Refresh")
        // FusionAuth: idt, and at for an access token
        || (token.TryGetPayloadValue<string>("tty", out var tty) && tty == "idt")
        // Cognito: id, and access for an access token
        || (token.TryGetPayloadValue<string>("token_use", out var tokenUse) && tokenUse == "id")
        // Pocket ID: id-token. Its access token has no type claim.
        || (token.TryGetPayloadValue<string>("type", out var type) && type == "id-token")
        // A back-channel logout token has an events claim (OpenID Connect Back-Channel Logout 1.0, section 2.4)
        || token.TryGetPayloadValue<object>("events", out _);
}
