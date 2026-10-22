using FullstackTemplate.Bff.Authentication;
using FullstackTemplate.Bff.Management;
using FullstackTemplate.Bff.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace FullstackTemplate.Bff.Logout;

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

    public static async Task HandleAsync(
        HttpContext context,
        BffSessionRequest session,
        IBffSessionStore store,
        BffTicketStore ticketStore,
        RefreshTokenRevocation revocation)
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

        // Delete the row first, so that a late write of a request that runs now cannot bring it back. Revoke the
        // refresh token of the deleted row, not the one that this request read: a refresh in another request can
        // write new tokens between the read and the delete.
        if (signedIn
            && session.Key is { } key
            && await store.DeleteAsync(key, context.RequestAborted) is { } deleted
            && ticketStore.RefreshTokenOf(deleted) is { Length: > 0 } refreshToken)
            await revocation.RevokeAsync(refreshToken, context.RequestAborted);

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (string.IsNullOrWhiteSpace(returnUrl))
            returnUrl = context.Request.PathBase.HasValue ? context.Request.PathBase.Value! : "/";

        // The handler sends the browser to the end_session_endpoint with id_token_hint, and back to returnUrl
        // through /signout-callback-oidc.
        await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties { RedirectUri = returnUrl });
    }
}
