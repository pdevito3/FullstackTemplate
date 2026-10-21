using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Login;

/// <summary>
/// A 400 for a request to the OIDC callback path that is not a GET. The BFF asks for <c>response_mode=query</c>,
/// so a response in a form POST is not the one that it asked for (OAuth 2.1 section 4.1.2).
/// <para>
/// This must be a middleware before <c>UseAuthentication</c>. The handler fails a POST with no form before
/// <c>OnMessageReceived</c> runs, and a failure there gives a 500.
/// </para>
/// </summary>
public static class CallbackMethodGuard
{
    private static readonly ILogger Logger = Log.ForContext(typeof(CallbackMethodGuard));

    public static IApplicationBuilder UseCallbackMethodGuard(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var callbackPath = context.RequestServices.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
                .Get(OpenIdConnectDefaults.AuthenticationScheme).CallbackPath;

            if (context.Request.Path.Equals(callbackPath, StringComparison.OrdinalIgnoreCase) && !HttpMethods.IsGet(context.Request.Method))
            {
                Logger.Here().Information("Rejected a {Method} request to the OIDC callback {Path}", context.Request.Method, callbackPath.Value);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            await next(context);
        });
}
