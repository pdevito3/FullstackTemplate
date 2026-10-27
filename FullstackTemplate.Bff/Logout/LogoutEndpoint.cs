using FullstackTemplate.Bff.Authentication;
using FullstackTemplate.Bff.Features;
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
/// claim of <c>/bff/user</c>. A logout with the cookie of an ended BFF session has no <c>sid</c> to check. The
/// cookie is <c>SameSite=Strict</c>, so a link from another site cannot send it to the logout of the provider.
/// </para>
/// </summary>
public static class LogoutEndpoint
{
    public const string Path = "/bff/logout";

    public const string EndSessionWithoutIdTokenHintKey = "Auth:EndSessionWithoutIdTokenHint";

    public static async Task HandleAsync(
        HttpContext context,
        BffSessionRequest session,
        IBffSessionStore store,
        BffTicketStore ticketStore,
        RefreshTokenRevocation revocation,
        ProviderFeatures features,
        SpaOrigin spaOrigin,
        IConfiguration configuration)
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

        if (ReturnUrl.Read(context.Request) is not { } returnUrl)
        {
            await ReturnUrl.WriteInvalidAsync(context);
            return;
        }

        // Delete the row first, so that a late write of a request that runs now cannot bring it back. Revoke the
        // refresh token of the deleted row, not the one that this request read: a refresh in another request can
        // write new tokens between the read and the delete.
        if (signedIn && session.Key is { } key && await store.DeleteAsync(key, context.RequestAborted) is { } deleted)
        {
            Telemetry.RecordSessionsEnded(SessionEndReason.Logout);

            if (ticketStore.RefreshTokenOf(deleted) is { Length: > 0 } refreshToken)
                await revocation.RevokeAsync(refreshToken, context.RequestAborted);
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var redirectUri = spaOrigin.Resolve(returnUrl);

        // With no BFF session, there is no ID token for id_token_hint, and the identity provider can refuse the
        // logout. With no end_session_endpoint, the BFF session is still gone, and the IdP session stays.
        var hasCookieOfEndedSession = !signedIn && context.Request.Cookies.ContainsKey(AuthExtensions.SessionCookieName);
        var endSession = features.Active.EndSession
            && (signedIn || (hasCookieOfEndedSession && configuration.GetValue<bool>(EndSessionWithoutIdTokenHintKey)));
        if (!endSession)
        {
            context.Response.Redirect(redirectUri);
            return;
        }

        // The handler sends the browser to the end_session_endpoint with id_token_hint, or with client_id when there
        // is no ID token, and back to returnUrl through /signout-callback-oidc.
        await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties { RedirectUri = redirectUri });
    }
}
