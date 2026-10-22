using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.ApiEndpoints;

/// <summary>
/// The CSRF defense of the BFF API endpoints. A cross-site page cannot send a custom header without a CORS
/// preflight, so a request with <c>X-CSRF: 1</c> comes from a page of an allowed origin.
/// <para>
/// The check applies to every HTTP method, GET too, and to anonymous requests. On failure the response is a
/// 401 with an empty body and no <c>WWW-Authenticate</c> header, and the request ends. It must run after
/// <c>UseRouting</c>, because it reads the endpoint metadata, and before <c>UseAuthorization</c>.
/// </para>
/// </summary>
public sealed class CsrfHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CSRF";
    public const string HeaderValue = "1";

    private static readonly ILogger Logger = Log.ForContext<CsrfHeaderMiddleware>();

    public Task InvokeAsync(HttpContext context)
    {
        if (context.IsBffApiEndpoint() && !HasCsrfHeader(context.Request))
        {
            Logger.Here().Warning("Rejected a request to {Path} with no valid {Header} header",
                LogValue.Clean(context.Request.Path.Value), HeaderName);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        return next(context);
    }

    /// <summary>
    /// True when the first <c>X-CSRF</c> value is exactly <c>1</c>.
    /// </summary>
    public static bool HasCsrfHeader(HttpRequest request) =>
        string.Equals(request.Headers[HeaderName].FirstOrDefault(), HeaderValue, StringComparison.Ordinal);
}

public static class CsrfHeaderMiddlewareExtensions
{
    /// <summary>
    /// Adds the <see cref="CsrfHeaderMiddleware"/>. Call it after <c>UseRouting</c> and before <c>UseAuthorization</c>.
    /// </summary>
    public static IApplicationBuilder UseCsrfHeaderCheck(this IApplicationBuilder app) =>
        app.UseMiddleware<CsrfHeaderMiddleware>();
}
