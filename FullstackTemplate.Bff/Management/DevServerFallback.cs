using FullstackTemplate.Bff.Proxy;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace FullstackTemplate.Bff.Management;

/// <summary>
/// In a local run the Vite dev server serves the SPA. A GET on the BFF origin that no endpoint matches, such as a
/// deep link, goes to the same path and query on the dev server.
/// </summary>
public static class DevServerFallback
{
    private static readonly PathString BffPrefix = "/bff";

    /// <summary>
    /// Must run after <c>UseRouting</c>, because it acts only when routing found no endpoint. A request with an
    /// endpoint, or with a 405 for the wrong method, goes on as before.
    /// </summary>
    public static IApplicationBuilder UseDevServerFallback(this IApplicationBuilder app, SpaOrigin spaOrigin) =>
        app.Use(async (context, next) =>
        {
            var oidc = context.RequestServices.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
                .Get(OpenIdConnectDefaults.AuthenticationScheme);
            var request = context.Request;
            var target = context.GetEndpoint() is null
                ? Target(spaOrigin, request.Method, request.Path, request.QueryString,
                    [ApiProxy.Prefix, BffPrefix, oidc.CallbackPath, oidc.SignedOutCallbackPath])
                : null;
            if (target is null)
            {
                await next(context);
                return;
            }

            context.Response.Redirect(target);
        });

    /// <summary>
    /// The dev server URL for a request that no endpoint matches, or null when it keeps its 404. Vite proxies
    /// <paramref name="bffPaths"/> to the BFF, so a redirect of an unknown path that starts with one of them would
    /// loop. The health probes also stay on the BFF. A path that is not a <see cref="LocalUrl"/>, such as
    /// <c>//host</c>, also keeps its 404.
    /// </summary>
    private static string? Target(SpaOrigin spaOrigin, string method, PathString path, QueryString query, IEnumerable<PathString> bffPaths)
    {
        if (spaOrigin.DevServer is null
            || !HttpMethods.IsGet(method)
            || Extensions.IsHealthProbe(path)
            // A raw prefix, not a path segment, because Vite proxies each URL that starts with the prefix, such as
            // /api-docs or /bff%2Fx. The match ignores case, so it also covers Vite's case-sensitive match.
            || bffPaths.Any(bffPath => path.Value!.StartsWith(bffPath.Value!, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return LocalUrl.TryParse(path.ToUriComponent() + query.ToUriComponent(), out var localUrl) ? spaOrigin.Resolve(localUrl) : null;
    }
}
