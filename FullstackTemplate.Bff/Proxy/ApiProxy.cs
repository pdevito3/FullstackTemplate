using System.Net.Http.Headers;
using Duende.AccessTokenManagement.OpenIdConnect;
using FullstackTemplate.Bff.ApiEndpoints;
using Serilog;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Proxy;

/// <summary>
/// The proxy from <c>/api/{**catch-all}</c> to the server API, with the user access token of the BFF session.
/// </summary>
public static class ApiProxy
{
    public const string Prefix = "/api";

    private static readonly ILogger Logger = Log.ForContext(typeof(ApiProxy));

    /// <summary>
    /// Maps the forwarder. <paramref name="apiBaseUrl"/> ends with <c>/api</c>, because the forwarder strips the
    /// local prefix: <c>/api/v1/x</c> goes to <c>&lt;server&gt;/api/v1/x</c>.
    /// <para>
    /// The transforms: remove <c>Cookie</c>, strip the prefix, and attach the user token. The YARP defaults also
    /// apply: the other request headers go through, <c>X-Forwarded-For</c>, <c>-Proto</c>, <c>-Host</c>, and
    /// <c>-Prefix</c> are set, <c>Forwarded</c> is removed, and the response headers go back as they are. A
    /// <c>Set-Cookie</c> that the BFF writes in the request, such as a cookie renewed by a token refresh, stays,
    /// because YARP appends the API headers to the headers that the response already has.
    /// </para>
    /// </summary>
    public static IEndpointConventionBuilder MapApiProxy(this IEndpointRouteBuilder endpoints, Uri apiBaseUrl) =>
        endpoints.MapForwarder($"{Prefix}/{{**catch-all}}", apiBaseUrl.ToString(), ForwarderRequestConfig.Empty, transforms =>
            {
                transforms.AddRequestHeaderRemove("Cookie");
                transforms.AddPathRemovePrefix(Prefix);
                transforms.AddRequestTransform(AttachUserAccessTokenAsync);
            })
            .AsBffApiEndpoint();

    /// <summary>
    /// Attaches the user access token, after a refresh when it is about to expire. An anonymous request, or a
    /// request whose refresh failed, goes to the API with no token, so the API decides the status code. The
    /// BFF session stays after a failed refresh.
    /// </summary>
    public static async ValueTask AttachUserAccessTokenAsync(RequestTransformContext context)
    {
        var httpContext = context.HttpContext;
        if (httpContext.User.Identity?.IsAuthenticated != true)
            return;

        try
        {
            var result = await httpContext.GetUserAccessTokenAsync(ct: httpContext.RequestAborted);
            if (!result.WasSuccessful(out var token, out var failure))
            {
                Logger.Here().Warning("Forwarded {Path} with no access token, because the token request failed with {Error}",
                    LogValue.Clean(httpContext.Request.Path.Value), failure.Error);
                return;
            }

            if (string.Equals(token.AccessTokenType?.ToString(), "DPoP", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Here().Error("Forwarded {Path} with no access token, because the BFF does not send DPoP tokens yet",
                    LogValue.Clean(httpContext.Request.Path.Value));
                return;
            }

            context.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken.ToString());
        }
        catch (Exception ex)
        {
            // YARP turns the exception into a 502.
            Logger.Here().Error(ex, "Failed to attach the access token to {Path}", LogValue.Clean(httpContext.Request.Path.Value));
            throw;
        }
    }
}
