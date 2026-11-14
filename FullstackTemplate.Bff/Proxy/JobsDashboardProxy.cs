using FullstackTemplate.Bff.Authentication;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

namespace FullstackTemplate.Bff.Proxy;

/// <summary>
/// The proxy from <c>/backwave</c> to the jobs dashboard of the server, with the user access token of the BFF session.
/// </summary>
public static class JobsDashboardProxy
{
    public const string Prefix = "/backwave";

    private const string DPoPHeaderName = "DPoP";

    /// <summary>
    /// Maps the forwarder for the pages, the form posts, and the live stream of the dashboard. It keeps the path, so
    /// the links that the dashboard builds from its path base work. The server decides who is an operator, so the BFF
    /// checks only that a user signed in, and a browser with no session gets the login. It is not an API endpoint, so
    /// a navigation needs no <c>X-CSRF</c> header. The antiforgery token of the server guards each post.
    /// <para>
    /// The request transforms: remove the BFF session cookie and keep the other cookies, remove <c>Authorization</c>
    /// and <c>DPoP</c>, and attach the user token. The response transform removes a <c>Set-Cookie</c> of the server for
    /// the BFF session cookie and keeps the others, such as the antiforgery cookie of the dashboard.
    /// </para>
    /// </summary>
    public static IEndpointConventionBuilder MapJobsDashboardProxy(this IEndpointRouteBuilder endpoints, Uri serverBaseUrl) =>
        endpoints.MapForwarder($"{Prefix}/{{**catch-all}}", serverBaseUrl.ToString(), ForwarderRequestConfig.Empty, transforms =>
            {
                transforms.AddRequestTransform(RemoveSessionCookies);
                transforms.AddRequestHeaderRemove(HeaderNames.Authorization);
                transforms.AddRequestHeaderRemove(DPoPHeaderName);
                // After the removal of Authorization, because it sets the header
                transforms.AddRequestTransform(ApiProxy.AttachUserAccessTokenAsync);
                transforms.AddResponseTransform(RemoveSessionSetCookies);
            })
            .RequireAuthorization();

    /// <summary>
    /// True for the BFF session cookie and its chunks. The cookie handler splits a large cookie into <c>__Host-bffC1</c>,
    /// <c>__Host-bffC2</c>, and so on.
    /// </summary>
    public static bool IsSessionCookie(string name) =>
        name.StartsWith(AuthExtensions.SessionCookieName, StringComparison.Ordinal);

    /// <summary>
    /// YARP copies the <c>Cookie</c> header as the browser sent it. The transform writes it again with no BFF session
    /// cookie, so the server never sees the session. The other pairs keep their bytes.
    /// </summary>
    private static ValueTask RemoveSessionCookies(RequestTransformContext context)
    {
        var headers = context.ProxyRequest.Headers;
        headers.Remove(HeaderNames.Cookie);
        var kept = context.HttpContext.Request.Headers.Cookie
            .SelectMany(header => (header ?? "").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Where(pair => !IsSessionCookie(pair.Split('=', 2)[0].Trim()))
            .ToList();
        if (kept.Count > 0)
            headers.TryAddWithoutValidation(HeaderNames.Cookie, string.Join("; ", kept));
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Removes each <c>Set-Cookie</c> of the server that names the BFF session cookie. YARP appends the server values to
    /// the values that the BFF wrote in this request, such as a session cookie renewed by a token refresh, so only the
    /// last equal value is removed.
    /// </summary>
    private static ValueTask RemoveSessionSetCookies(ResponseTransformContext context)
    {
        if (context.ProxyResponse is null || !context.ProxyResponse.Headers.TryGetValues(HeaderNames.SetCookie, out var serverValues))
            return ValueTask.CompletedTask;

        var headers = context.HttpContext.Response.Headers;
        var values = headers.SetCookie.ToList();
        foreach (var serverValue in serverValues)
        {
            if (!SetCookieHeaderValue.TryParse(serverValue, out var cookie) || !IsSessionCookie(cookie.Name.ToString()))
                continue;

            var index = values.LastIndexOf(serverValue);
            if (index >= 0)
                values.RemoveAt(index);
        }

        headers.SetCookie = values.Count == 0 ? StringValues.Empty : new StringValues(values.ToArray());
        return ValueTask.CompletedTask;
    }
}
