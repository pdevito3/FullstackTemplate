using System.Text.Encodings.Web;
using FullstackTemplate.Bff.Management;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http.Extensions;

namespace FullstackTemplate.Bff.Proxy;

/// <summary>
/// What a request to the jobs dashboard with no BFF session gets. The other endpoints get the default result.
/// <para>
/// The session cookie is <c>SameSite=Strict</c>, so a browser does not send it on a cross-site navigation, such as
/// the redirect from the identity provider after the login, or a link in an email. The dashboard is a page that needs
/// the session, so a challenge would send a signed-in browser to the identity provider again and again. A cross-site
/// navigation thus gets a page that loads the same URL again from its own origin, and that load sends the cookie.
/// Each other request with no session gets the login, which comes back to the dashboard URL on the origin of the SPA.
/// The load again has <see cref="LoadedAgainParameter"/> in its URL, so with no session it gets the login, not the page
/// again.
/// </para>
/// <para>
/// So a GET navigation of the dashboard from another site loads the page with the session, as with a <c>Lax</c>
/// cookie. A page of the dashboard only reads. Each post of the dashboard still needs the cookie, and the antiforgery
/// token of the server.
/// </para>
/// </summary>
public sealed class JobsDashboardLogin(SpaOrigin spaOrigin) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    /// <summary>
    /// The endpoint metadata of the dashboard proxy.
    /// </summary>
    public sealed class Marker;

    /// <summary>
    /// The query parameter of the URL that the page loads again. The proxy removes it from the request to the server,
    /// and the login removes it from the URL that the login comes back to.
    /// </summary>
    public const string LoadedAgainParameter = "bff_loaded_again";

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Challenged || context.GetEndpoint()?.Metadata.GetMetadata<Marker>() is null)
            return _default.HandleAsync(next, context, policy, authorizeResult);

        return IsCrossSiteNavigation(context.Request) && !context.Request.Query.ContainsKey(LoadedAgainParameter)
            ? WriteLoadAgainPageAsync(context)
            : ChallengeAsync(context);
    }

    // Only Fetch Metadata tells a cross-site navigation apart, so a browser with no Fetch Metadata gets the login.
    private static bool IsCrossSiteNavigation(HttpRequest request) =>
        HttpMethods.IsGet(request.Method)
        && request.Headers["Sec-Fetch-Site"] == "cross-site"
        && request.Headers["Sec-Fetch-Mode"] == "navigate"
        && request.Headers["Sec-Fetch-Dest"] == "document";

    private static Task WriteLoadAgainPageAsync(HttpContext context)
    {
        var request = context.Request;
        var url = HtmlEncoder.Default.Encode(UriHelper.BuildRelative(request.PathBase, request.Path,
            request.QueryString.Add(LoadedAgainParameter, "1")));
        var response = context.Response;
        response.ContentType = "text/html; charset=utf-8";
        response.Headers.CacheControl = "no-store";
        // The link is for a browser that does not refresh
        return response.WriteAsync($"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta http-equiv="refresh" content="0; url={url}">
            <title>Background jobs</title>
            </head>
            <body>
            <p><a href="{url}">Open the jobs dashboard</a></p>
            </body>
            </html>
            """, context.RequestAborted);
    }

    private Task ChallengeAsync(HttpContext context)
    {
        var request = context.Request;
        var query = request.Query.ContainsKey(LoadedAgainParameter)
            ? QueryString.Create(request.Query.Where(p => !string.Equals(p.Key, LoadedAgainParameter, StringComparison.OrdinalIgnoreCase)))
            : request.QueryString;
        var url = UriHelper.BuildRelative(request.PathBase, request.Path, query);
        // As for /bff/login: the URL travels in the state of the authorize URL
        if (url.Length > ReturnUrl.MaxLength)
            url = UriHelper.BuildRelative(request.PathBase, JobsDashboardProxy.Prefix);

        var properties = new AuthenticationProperties { RedirectUri = LocalUrl.TryParse(url, out var page) ? spaOrigin.Resolve(page) : null };
        return context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, properties);
    }
}
