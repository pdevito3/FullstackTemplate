using System.Net.Http.Headers;
using Duende.AccessTokenManagement.OpenIdConnect;
using FullstackTemplate.Bff.ApiEndpoints;
using FullstackTemplate.Bff.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
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

    private const string DPoPHeaderName = "DPoP";

    private static readonly ILogger Logger = Log.ForContext(typeof(ApiProxy));

    /// <summary>
    /// Maps the forwarder. <paramref name="apiBaseUrl"/> ends with <c>/api</c>, because the forwarder strips the
    /// local prefix: <c>/api/v1/x</c> goes to <c>&lt;server&gt;/api/v1/x</c>.
    /// <para>
    /// The request transforms: remove <c>Cookie</c>, remove <c>Authorization</c> and <c>DPoP</c> (the BFF is the only
    /// source of tokens), strip the prefix, and attach the user token. The response transform removes the
    /// <c>Set-Cookie</c> values of the API, so that the API cannot replace the BFF session cookie. The YARP defaults
    /// also apply: the other request headers go through, <c>X-Forwarded-For</c>, <c>-Proto</c>, <c>-Host</c>, and
    /// <c>-Prefix</c> are set, <c>Forwarded</c> is removed, and the other response headers go back as they are.
    /// </para>
    /// </summary>
    public static IEndpointConventionBuilder MapApiProxy(this IEndpointRouteBuilder endpoints, Uri apiBaseUrl) =>
        endpoints.MapForwarder($"{Prefix}/{{**catch-all}}", apiBaseUrl.ToString(), ForwarderRequestConfig.Empty, transforms =>
            {
                transforms.AddRequestHeaderRemove(HeaderNames.Cookie);
                transforms.AddRequestHeaderRemove(HeaderNames.Authorization);
                transforms.AddRequestHeaderRemove(DPoPHeaderName);
                transforms.AddPathRemovePrefix(Prefix);
                // After the removal of Authorization, because it sets the header
                transforms.AddRequestTransform(AttachUserAccessTokenAsync);
                transforms.AddResponseTransform(RemoveApiSetCookies);
            })
            .AsBffApiEndpoint();

    /// <summary>
    /// Removes the <c>Set-Cookie</c> values of the proxied response. A <c>Set-Cookie</c> that the BFF wrote in this
    /// request, such as a BFF session cookie renewed by a token refresh, stays. YARP appends the API headers to the
    /// headers that the response already has, so a plain removal of the header would also remove the BFF values.
    /// </summary>
    public static ValueTask RemoveApiSetCookies(ResponseTransformContext context)
    {
        if (context.ProxyResponse is null || !context.ProxyResponse.Headers.TryGetValues(HeaderNames.SetCookie, out var apiValues))
            return ValueTask.CompletedTask;

        var headers = context.HttpContext.Response.Headers;
        var values = headers.SetCookie.ToList();
        var removed = 0;
        foreach (var apiValue in apiValues)
        {
            // YARP appends, so the value of the API is the last one that is equal
            var index = values.LastIndexOf(apiValue);
            if (index < 0)
                continue;

            values.RemoveAt(index);
            removed++;
        }

        headers.SetCookie = values.Count == 0 ? StringValues.Empty : new StringValues(values.ToArray());
        if (removed > 0)
        {
            Logger.Here().Debug("Removed {Count} Set-Cookie values of the API response from {Path}",
                removed, LogValue.Clean(context.HttpContext.Request.Path.Value));
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Attaches the user access token, after a refresh when it is about to expire. An anonymous request goes to
    /// the API with no token, so the API decides the status code. The rules of a failed refresh (section 3.7 of
    /// the spec):
    /// <list type="bullet">
    ///   <item><c>invalid_grant</c>, or a BFF session that ended during the refresh lock: the refresh lock deleted
    ///   the row. The transform signs out the cookie and forwards with no token, so the next <c>/bff/user</c>
    ///   gives a 401.</item>
    ///   <item>Any other failure, such as a network error, a 5xx, a token request timeout, or a lock timeout, is
    ///   temporary: the BFF session stays. The transform sends the current access token while it is valid, and no
    ///   token after that. The next request tries the refresh again.</item>
    /// </list>
    /// The refresh lock logs and counts each failed refresh, so the transform logs at most at
    /// <c>Information</c>.
    /// </summary>
    public static async ValueTask AttachUserAccessTokenAsync(RequestTransformContext context)
    {
        var httpContext = context.HttpContext;
        if (httpContext.User.Identity?.IsAuthenticated != true)
            return;

        // The browser chooses the path, so clean it of CR, LF, and the other control characters before a log
        var path = LogValue.Clean(httpContext.Request.Path.Value);
        try
        {
            // No RequestAborted: a refresh can be shared with other requests, and the refresh lock stops only the
            // wait of a request whose client went away, not the refresh.
            var result = await httpContext.GetUserAccessTokenAsync();
            if (!result.WasSuccessful(out var token, out var failure))
            {
                if (RefreshLock.EndsSession(failure.Error))
                {
                    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    Logger.Here().Information("Forwarded {Path} with no access token, because the BFF session ended after the token request failed with {Error}",
                        path, failure.Error);
                    return;
                }

                token = await CurrentValidTokenAsync(httpContext);
                if (token is null)
                {
                    Logger.Here().Information("Forwarded {Path} with no access token, because the token request failed with {Error} and the current access token expired",
                        path, failure.Error);
                    return;
                }

                Logger.Here().Debug("Forwarded {Path} with the current access token, because the token request failed with {Error}",
                    path, failure.Error);
            }

            if (string.Equals(token.AccessTokenType?.ToString(), "DPoP", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Here().Error("Forwarded {Path} with no access token, because the BFF does not send DPoP tokens yet", path);
                return;
            }

            context.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken.ToString());
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client left. That is not a fault of the BFF.
            Logger.Here().Debug("The client aborted {Path} while the BFF attached the access token", path);
            throw;
        }
        catch (Exception ex)
        {
            // YARP turns the exception into a 502.
            Logger.Here().Error(ex, "Failed to attach the access token to {Path}", path);
            throw;
        }
    }

    /// <summary>
    /// The access token of the BFF session as the request read it, or null when it has expired.
    /// </summary>
    private static async Task<UserToken?> CurrentValidTokenAsync(HttpContext httpContext)
    {
        var services = httpContext.RequestServices;
        var current = await services.GetRequiredService<IUserTokenStore>()
            .GetTokenAsync(httpContext.User, ct: httpContext.RequestAborted);
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        return current.WasSuccessful(out var stored) && stored.TokenForSpecifiedParameters is { } token && token.Expiration > now
            ? token
            : null;
    }
}
