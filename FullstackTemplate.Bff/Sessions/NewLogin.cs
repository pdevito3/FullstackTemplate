using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace FullstackTemplate.Bff.Sessions;

/// <summary>
/// A new login always gets a new key, so that a renewal never puts the ticket of a new login, with a new
/// <c>sub</c> or <c>sid</c>, on an old key.
/// <para>
/// The cookie handler signs in on the key of the request cookie when that key has a row, and it keeps that key
/// after a sign-out in the same request. Thus the callback must end the existing BFF session before the cookie
/// handler reads the cookie: delete its row, remove the cookie from the request, and sign out the cookie.
/// </para>
/// </summary>
public static class NewLogin
{
    /// <summary>
    /// The OIDC <c>OnTicketReceived</c> step. It runs before the sign-in of the new ticket.
    /// </summary>
    public static async Task EndExistingSessionAsync(TicketReceivedContext context)
    {
        var httpContext = context.HttpContext;
        var cookieOptions = httpContext.RequestServices.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var cookieName = cookieOptions.Cookie.Name!;

        var cookie = cookieOptions.CookieManager.GetRequestCookie(httpContext, cookieName);
        if (string.IsNullOrEmpty(cookie))
            return;

        var key = cookieOptions.TicketDataFormat.Unprotect(cookie)?.Principal.FindFirst(SessionKey.ClaimType)?.Value;
        if (key is not null)
            await httpContext.RequestServices.GetRequiredService<IBffSessionStore>().DeleteAsync(key, httpContext.RequestAborted);

        RemoveRequestCookie(httpContext.Request, cookieName);
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Removes the cookie, and its chunks, from the <c>Cookie</c> header. The other cookies stay as they are.
    /// </summary>
    private static void RemoveRequestCookie(HttpRequest request, string cookieName)
    {
        var kept = request.Headers.Cookie
            .SelectMany(header => (header ?? "").Split(';'))
            .Select(pair => pair.Trim())
            .Where(pair => pair.Length > 0 && !IsSessionCookie(pair.Split('=', 2)[0], cookieName))
            .ToArray();

        request.Headers.Cookie = kept.Length == 0 ? StringValues.Empty : new StringValues(string.Join("; ", kept));
    }

    // The chunking cookie manager names the chunks <name>C1, <name>C2, and so on.
    private static bool IsSessionCookie(string name, string cookieName) =>
        name == cookieName
        || (name.Length > cookieName.Length + 1
            && name.StartsWith(cookieName + "C", StringComparison.Ordinal)
            && name[(cookieName.Length + 1)..].All(char.IsAsciiDigit));
}
