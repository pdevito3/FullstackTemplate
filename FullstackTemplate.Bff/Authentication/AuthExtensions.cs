using Duende.AccessTokenManagement.OpenIdConnect;
using FullstackTemplate.Bff.ApiEndpoints;
using FullstackTemplate.Bff.DPoP;
using FullstackTemplate.Bff.Features;
using FullstackTemplate.Bff.Login;
using FullstackTemplate.Bff.Sessions;
using FullstackTemplate.Bff.User;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

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
    /// The algorithm of the ID tokens and logout tokens, as the client registration sets it. The default is RS256.
    /// </summary>
    public const string IdTokenSigningAlgorithmKey = "Auth:IdTokenSigningAlgorithm";

    /// <summary>
    /// The AppContext switch of Microsoft.IdentityModel that makes a refresh of the discovery document and the
    /// signing keys block the caller, instead of a refresh in the background.
    /// </summary>
    public const string UpdateConfigAsBlockingSwitch = "Switch.Microsoft.IdentityModel.UpdateConfigAsBlocking";

    /// <summary>
    /// Adds the cookie and OIDC handlers, token management, and the BFF API status code rules.
    /// Reads Auth:Authority, Auth:ClientId, Auth:ClientSecret, Auth:RequireHttpsMetadata,
    /// Auth:NameClaimType, and Auth:RoleClaimType from configuration. The BFF decides the provider features from
    /// Auth:Features, Auth:ClientAuthentication, and the discovery document at startup.
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
        var useAudienceAsScope = configuration.GetValue("Auth:UseAudienceAsScope", true);
        var requestOfflineAccess = configuration.GetValue("Auth:RequestOfflineAccess", true);
        var refreshBeforeExpirationSeconds = configuration.GetValue("Auth:RefreshBeforeExpirationSeconds", 120);
        // The id_token_signed_response_alg of the client registration. Core 3.1.3.7 step 7: the ID token alg is
        // this value, by default RS256. The logout tokens get the same check.
        var idTokenSigningAlgorithm = configuration[IdTokenSigningAlgorithmKey] ?? SecurityAlgorithms.RsaSha256;

        // Signing key rollover (Core 10.1.1): an ID token with an unknown key makes the token handler refresh the
        // keys and validate again. By default the ConfigurationManager refreshes in the background, so the retry
        // still has the old keys and the first login after a rollover fails. With this switch the refresh blocks.
        AppContext.SetSwitch(UpdateConfigAsBlockingSwitch, true);

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
                // Each call that needs the BFF session is same-site. The first page load after the redirect from the
                // identity provider is cross-site, so it has no cookie, but its HTML needs none.
                options.Cookie.SameSite = SameSiteMode.Strict;
                // Also in development: browsers treat http://localhost as a secure context
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

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

                // Lax, so that the browser sends them on the top-level GET from the identity provider to the callback.
                // The default is None.
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.NonceCookie.SameSite = SameSiteMode.Lax;

                // No x-client-SKU and x-client-ver on the authorize URL: a pushed request then has only client_id and
                // request_uri, and the provider does not learn the library version
                options.DisableTelemetry = true;

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
                    RoleClaimType = roleClaimType,
                    ValidAlgorithms = [idTokenSigningAlgorithm],
                };

                // Front-channel logout is off: the handler maps no /signout-oidc. Only back-channel logout tells the
                // BFF that an IdP session ended.
                options.RemoteSignOutPath = PathString.Empty;

                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    // Connect the login and callback traces across the identity provider redirect.
                    LoginTraceLink.Capture(context.Properties);
                    LoginPrompt.CopyToAuthorizeRequest(context);
                    return Task.CompletedTask;
                };

                options.Events.OnMessageReceived = context =>
                {
                    // The iss check comes first, so that the BFF does not trust an error response that fails it
                    AuthorizationResponseIssuer.Check(context);
                    return context.Result is null ? LoginPrompt.RedirectOnPromptError(context) : Task.CompletedTask;
                };

                options.Events.OnPushAuthorization = TokenClientAuthentication.PushAuthorizationAsync;
                options.Events.OnAuthorizationCodeReceived = TokenClientAuthentication.RedeemCodeAsync;
                options.Events.OnTokenValidated = IdTokenChecks.ValidateAsync;
                options.Events.OnRemoteFailure = CallbackRejection.WriteProblemAsync;

                options.Events.OnTicketReceived = context =>
                {
                    LoginTraceLink.LinkCurrentActivity(context.Properties);
                    return NewLogin.EndExistingSessionAsync(context);
                };
            });

        // Cognito has a logout endpoint, but its discovery document has no end_session_endpoint.
        // Its logout endpoint reads client_id and logout_uri, and it sends no state back.
        // Without state, the handler does not redirect from the callback, so the callback redirects to SignedOutRedirectUri.
        services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<IConfiguration>((options, settings) =>
            {
                var logoutEndpoint = settings["Auth:LogoutEndpoint"];
                if (string.IsNullOrEmpty(logoutEndpoint))
                    return;

                var onRedirectToIdentityProviderForSignOut = options.Events.OnRedirectToIdentityProviderForSignOut;
                options.Events.OnRedirectToIdentityProviderForSignOut = context =>
                {
                    context.ProtocolMessage.IssuerAddress = logoutEndpoint;
                    context.ProtocolMessage.ClientId = context.Options.ClientId;
                    context.ProtocolMessage.SetParameter("logout_uri", context.ProtocolMessage.PostLogoutRedirectUri);
                    return onRedirectToIdentityProviderForSignOut(context);
                };

                var onSignedOutCallbackRedirect = options.Events.OnSignedOutCallbackRedirect;
                options.Events.OnSignedOutCallbackRedirect = context =>
                {
                    if (!string.IsNullOrEmpty(context.Properties?.RedirectUri))
                        return onSignedOutCallbackRedirect(context);

                    context.Response.Redirect(context.Options.SignedOutRedirectUri);
                    context.HandleResponse();
                    return Task.CompletedTask;
                };
            });

        // Auth:MetadataRefreshInterval and Auth:PinnedCertificatePath. Before token management, because it wraps the
        // backchannel handler that this sets.
        services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<IConfiguration>(IdentityProviderBackchannel.Configure);

        // After the post-configure of the OIDC handler, which makes the ConfigurationManager
        services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, DiscoveryIssuerCheck.Install);

        // After the OIDC options, because it wraps the OIDC events and the backchannel handler.
        services.AddOpenIdConnectAccessTokenManagement(options =>
        {
            options.RefreshBeforeExpiration = TimeSpan.FromSeconds(refreshBeforeExpirationSeconds);
        });
        // Auth:DPoPJsonWebKey with the final configuration. Under Auto, the feature decision turns it off again when
        // discovery lacks DPoP.
        services.AddOptions<UserTokenManagementOptions>()
            .Configure<IConfiguration>((options, configuration) => options.DPoPJsonWebKey = DPoPKey.ReadOrNull(configuration));
        RefreshIdTokenValidation.Register(services);
        RefreshFailureMetrics.Register(services);
        DefaultAccessTokenLifetime.AddToRefresh(services);
        services.AddOptions<RefreshOptions>()
            .BindConfiguration(RefreshOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        // Replaces the in-process lock of token management
        services.Replace(ServiceDescriptor.Singleton<IUserTokenRequestConcurrencyControl, RefreshLock>());

        services.AddOptions<ProviderFeatureOptions>().BindConfiguration(ProviderFeatureOptions.SectionName);
        services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<IOptions<ProviderFeatureOptions>>((oidc, features) =>
                oidc.PushedAuthorizationBehavior = ProviderFeatureOptions.ToPushedAuthorizationBehavior(features.Value.PushedAuthorization));
        services.AddSingleton<ProviderFeatures>();
        services.AddSingleton<TokenClientAuthentication>();
        services.AddHostedService<ProviderFeatureDiscovery>();

        services.AddBffApiStatusCodes();
        services.AddAuthorization();

        return services;
    }
}
