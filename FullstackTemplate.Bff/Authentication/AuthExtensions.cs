using Duende.AccessTokenManagement.OpenIdConnect;
using FullstackTemplate.Bff.ApiEndpoints;
using FullstackTemplate.Bff.Login;
using FullstackTemplate.Bff.Sessions;
using FullstackTemplate.Bff.User;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// Extension methods for configuring BFF authentication.
/// </summary>
public static class AuthExtensions
{
    /// <summary>
    /// The name of the BFF session cookie. The SPA and the e2e tests depend on it.
    /// </summary>
    public const string SessionCookieName = "__Host-bff";

    /// <summary>
    /// Adds the cookie and OIDC handlers, token management, and the BFF API status code rules.
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
        var useAudienceAsScope = configuration.GetValue("Auth:UseAudienceAsScope", true);
        var requestOfflineAccess = configuration.GetValue("Auth:RequestOfflineAccess", true);
        var logoutEndpoint = configuration["Auth:LogoutEndpoint"];
        var refreshBeforeExpirationSeconds = configuration.GetValue("Auth:RefreshBeforeExpirationSeconds", 120);

        services.AddAuthentication(options =>
            {
                // The cookie authenticates and forbids. OIDC challenges and signs out.
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = SessionCookieName;
                options.Cookie.HttpOnly = true;
                // Lax is required for OIDC redirects from external IdP
                options.Cookie.SameSite = SameSiteMode.Lax;
                // Ensure cookie works across ports in dev
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                options.Events.OnCheckSlidingExpiration = UserEndpoint.SuppressSlideWhenRequested;
            })
            .AddOpenIdConnect(options =>
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

                // Cognito rejects offline_access and returns a refresh token without it
                if (requestOfflineAccess)
                {
                    options.Scope.Add("offline_access");
                }

                // Request the API audience scope to get access tokens with correct audience claim
                // Some providers (like Keycloak) require this; others (like FusionAuth) don't support custom scopes
                if (useAudienceAsScope && !string.IsNullOrEmpty(authAudience))
                {
                    options.Scope.Add(authAudience);
                }

                options.TokenValidationParameters = new()
                {
                    NameClaimType = nameClaimType,
                    RoleClaimType = roleClaimType
                };

                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    // Connect the login and callback traces across the identity provider redirect.
                    LoginTraceLink.Capture(context.Properties);
                    LoginPrompt.CopyToAuthorizeRequest(context);
                    return Task.CompletedTask;
                };

                options.Events.OnMessageReceived = LoginPrompt.RedirectOnPromptError;

                // Cognito has a logout endpoint, but its discovery document has no end_session_endpoint.
                // Its logout endpoint reads client_id and logout_uri, and it sends no state back.
                // Without state, the handler does not redirect from the callback, so the callback redirects to SignedOutRedirectUri.
                if (!string.IsNullOrEmpty(logoutEndpoint))
                {
                    options.Events.OnRedirectToIdentityProviderForSignOut = context =>
                    {
                        context.ProtocolMessage.IssuerAddress = logoutEndpoint;
                        context.ProtocolMessage.ClientId = authClientId;
                        context.ProtocolMessage.SetParameter("logout_uri", context.ProtocolMessage.PostLogoutRedirectUri);
                        return Task.CompletedTask;
                    };

                    options.Events.OnSignedOutCallbackRedirect = context =>
                    {
                        if (string.IsNullOrEmpty(context.Properties?.RedirectUri))
                        {
                            context.Response.Redirect(context.Options.SignedOutRedirectUri);
                            context.HandleResponse();
                        }
                        return Task.CompletedTask;
                    };
                }

                options.Events.OnTicketReceived = context =>
                {
                    LoginTraceLink.LinkCurrentActivity(context.Properties);
                    return NewLogin.EndExistingSessionAsync(context);
                };
            });

        // After the OIDC options, because it wraps the OIDC events and the backchannel handler.
        services.AddOpenIdConnectAccessTokenManagement(options =>
        {
            options.RefreshBeforeExpiration = TimeSpan.FromSeconds(refreshBeforeExpirationSeconds);
        });

        services.Configure<RevocationOptions>(options => options.Enabled = revokeRefreshTokenOnLogout);

        services.AddBffApiStatusCodes();
        services.AddAuthorization();

        return services;
    }
}
