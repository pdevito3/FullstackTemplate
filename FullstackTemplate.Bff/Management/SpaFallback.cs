using FullstackTemplate.Bff.Proxy;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace FullstackTemplate.Bff.Management;

/// <summary>
/// The SPA answer to a GET or a HEAD of a page that no endpoint matches, such as a deep link. In a local run the
/// Vite dev server serves the SPA, so the request goes to the same path and query on the dev server. A published
/// BFF serves <c>index.html</c> from <c>wwwroot</c>.
/// </summary>
public static class SpaFallback
{
    private static readonly PathString BffPrefix = "/bff";
    private static readonly PathString IndexHtml = "/index.html";

    /// <summary>
    /// Must run after <c>UseRouting</c>, because it acts only when routing found no endpoint. A request with an
    /// endpoint, or with a 405 for the wrong method, goes on as before.
    /// </summary>
    public static IApplicationBuilder UseSpaFallback(this IApplicationBuilder app, SpaOrigin spaOrigin)
    {
        // Only when published, because a dev BFF has no wwwroot, and the static files middleware warns about that
        var serveIndexHtml = spaOrigin.DevServer is null ? app.New().UseStaticFiles().Build() : null;

        return app.Use(async (context, next) =>
        {
            var oidc = context.RequestServices.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
                .Get(OpenIdConnectDefaults.AuthenticationScheme);
            var request = context.Request;
            if (context.GetEndpoint() is not null
                || !IsPage(request.Method, request.Path, [ApiProxy.Prefix, BffPrefix, oidc.CallbackPath, oidc.SignedOutCallbackPath]))
            {
                await next(context);
                return;
            }

            if (spaOrigin.DevServer is not null)
            {
                if (LocalUrl.TryParse(request.Path.ToUriComponent() + request.QueryString.ToUriComponent(), out var localUrl))
                {
                    context.Response.Redirect(spaOrigin.Resolve(localUrl));
                    return;
                }
            }
            // A missing file, such as an old bundle, keeps its 404
            else if (!Path.HasExtension(request.Path.Value))
            {
                request.Path = IndexHtml;
                await serveIndexHtml!(context);
                return;
            }

            await next(context);
        });
    }

    /// <summary>
    /// Whether the request can be a page of the SPA. Vite proxies <paramref name="bffPaths"/> to the BFF, so a
    /// redirect of an unknown path that starts with one of them would loop. A published BFF keeps the same 404s for
    /// these prefixes. The health probes also stay on the BFF.
    /// </summary>
    private static bool IsPage(string method, PathString path, IEnumerable<PathString> bffPaths) =>
        (HttpMethods.IsGet(method) || HttpMethods.IsHead(method))
        && !Extensions.IsHealthProbe(path)
        // A raw prefix, not a path segment, because Vite proxies each URL that starts with the prefix, such as
        // /api-docs or /bff%2Fx. The match ignores case, so it also covers Vite's case-sensitive match.
        && !bffPaths.Any(bffPath => path.Value!.StartsWith(bffPath.Value!, StringComparison.OrdinalIgnoreCase));
}
