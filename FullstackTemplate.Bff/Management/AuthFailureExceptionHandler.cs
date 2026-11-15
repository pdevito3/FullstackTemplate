using System.Diagnostics;
using FullstackTemplate.Bff.Login;
using FullstackTemplate.Bff.Logout;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Management;

/// <summary>
/// Sends a browser navigation to a login, a logout, or an OIDC callback that throws, for example when the BFF session
/// store is down, to the <see cref="AuthFailurePage"/>. Each other request gets the problem of the exception handler.
/// </summary>
public sealed class AuthFailureExceptionHandler(IOptionsMonitor<OpenIdConnectOptions> oidcOptions) : IExceptionHandler
{
    private static readonly ILogger Logger = Log.ForContext<AuthFailureExceptionHandler>();

    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!AuthFailurePage.IsNavigation(httpContext.Request) || PageOf(httpContext.Request.Path) is not { } page)
            return ValueTask.FromResult(false);

        // In .NET 10, the exception handler middleware does not log an exception that an IExceptionHandler handles.
        Logger.Here().Error(exception, "A navigation to {Path} failed, so the browser goes to the page of the failure",
            LogValue.Clean(httpContext.Request.Path.Value));
        Telemetry.RecordError(Activity.Current, exception);
        page.Redirect(httpContext);
        return ValueTask.FromResult(true);
    }

    private AuthFailurePage? PageOf(PathString path)
    {
        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        if (path.Equals(LoginEndpoint.Path, StringComparison.OrdinalIgnoreCase) || path.Equals(options.CallbackPath, StringComparison.OrdinalIgnoreCase))
            return AuthFailurePage.SignIn;
        if (path.Equals(LogoutEndpoint.Path, StringComparison.OrdinalIgnoreCase) || path.Equals(options.SignedOutCallbackPath, StringComparison.OrdinalIgnoreCase))
            return AuthFailurePage.SignOut;
        return null;
    }
}
