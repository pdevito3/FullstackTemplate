using Duende.AccessTokenManagement.OpenIdConnect;
using FullstackTemplate.Bff.Management;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Logout;

public sealed class LogoutOptions
{
    /// <summary>
    /// Revoke the refresh token at the identity provider (RFC 7009) on logout. From
    /// <c>Auth:RevokeRefreshTokenOnLogout</c>, default true.
    /// </summary>
    public bool RevokeRefreshToken { get; set; } = true;
}

/// <summary>
/// <c>/bff/logout</c> ends the BFF session and starts RP-initiated logout at the identity provider.
/// <para>
/// The endpoint is a GET, so the <c>sid</c> query is its CSRF defense: a BFF session with a <c>sid</c> claim
/// needs the same value in the query. The SPA gets the URL with the <c>sid</c> from the <c>bff:logout_url</c>
/// claim of <c>/bff/user</c>.
/// </para>
/// </summary>
public static class LogoutEndpoint
{
    public const string Path = "/bff/logout";

    private static readonly ILogger Logger = Log.ForContext(typeof(LogoutEndpoint));

    public static async Task HandleAsync(
        HttpContext context,
        IOptions<LogoutOptions> options,
        IOpenIdConnectConfigurationService oidcConfiguration)
    {
        var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var signedIn = result.Succeeded && result.Principal?.Identity?.IsAuthenticated == true;

        if (signedIn)
        {
            var sessionId = result.Principal!.FindFirst("sid")?.Value;
            if (!string.IsNullOrWhiteSpace(sessionId)
                && !string.Equals(sessionId, context.Request.Query["sid"].FirstOrDefault(), StringComparison.Ordinal))
            {
                await ManagementProblem.WriteAsync(context, "Invalid Session ID", "sid", $"SessionId '{sessionId}' was invalid");
                return;
            }
        }

        var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(returnUrl) && !LocalUrl.IsLocal(returnUrl))
        {
            await ManagementProblem.WriteAsync(context, "Invalid return url", "returnUrl", $"ReturnUrl '{returnUrl}' was invalid");
            return;
        }

        if (signedIn && options.Value.RevokeRefreshToken)
            await RevokeRefreshTokenAsync(context, oidcConfiguration);

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (string.IsNullOrWhiteSpace(returnUrl))
            returnUrl = context.Request.PathBase.HasValue ? context.Request.PathBase.Value! : "/";

        // The handler sends the browser to the end_session_endpoint with id_token_hint, and back to returnUrl
        // through /signout-callback-oidc.
        await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties { RedirectUri = returnUrl });
    }

    /// <summary>
    /// Revokes the refresh token of the current user. A provider with no <c>revocation_endpoint</c> in
    /// discovery is skipped, so the logout still completes. Token management logs an error response of the
    /// provider and continues.
    /// </summary>
    private static async Task RevokeRefreshTokenAsync(HttpContext context, IOpenIdConnectConfigurationService oidcConfiguration)
    {
        var configuration = await oidcConfiguration.GetOpenIdConnectConfigurationAsync(ct: context.RequestAborted);
        if (configuration.RevocationEndpoint is null)
        {
            Logger.Here().Information("Skipped the refresh token revocation, because discovery has no revocation_endpoint");
            return;
        }

        await context.RevokeRefreshTokenAsync(ct: context.RequestAborted);
    }
}
