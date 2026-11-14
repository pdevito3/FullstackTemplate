using FullstackTemplate.Bff.Management;
using FullstackTemplate.Bff.Sessions;
using Microsoft.AspNetCore.Authentication;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Login;

/// <summary>
/// A callback to <c>/signin-oidc</c> that failed one of the BFF checks: the <c>iss</c> check or the strict
/// audience check. The BFF does not trust the response, and answers 400, or sends a navigation to the
/// <see cref="AuthFailurePage.SignIn"/> page.
/// </summary>
/// <param name="field">The parameter or claim that failed, for the problem response.</param>
/// <param name="reason">Why the check failed. It holds no token or code.</param>
public sealed class CallbackRejectedException(string field, string reason) : Exception(reason)
{
    public const string Title = "Invalid authorization response";

    public string Field { get; } = field;
}

public static class CallbackRejection
{
    private static readonly ILogger Logger = Log.ForContext(typeof(CallbackRejection));

    /// <summary>
    /// The <c>OnRemoteFailure</c> event. A failure of a BFF check becomes a 400 problem response for its field.
    /// Each other failure (an invalid <c>state</c>, a missing correlation cookie, or an <c>error</c> from the
    /// identity provider) becomes a 400 problem response for the <c>callback</c> field, not the handler default 500.
    /// </summary>
    public static async Task RefuseAsync(RemoteFailureContext context)
    {
        context.HandleResponse();

        if (context.Failure is CallbackRejectedException rejection)
        {
            Logger.Here().Warning("Rejected an OIDC callback: {Reason}", rejection.Message);
            await ManagementProblem.RefuseAsync(context.HttpContext, AuthFailurePage.SignIn, CallbackRejectedException.Title, rejection.Field, rejection.Message);
            return;
        }

        // The messages of the OIDC handler name the failed step. They hold no token, code, or state value.
        Logger.Here().Warning("An OIDC callback failed: {FailureType}: {Reason}",
            context.Failure?.GetType().Name, context.Failure?.Message);
        await WriteLoginFailedAsync(context.HttpContext);
    }

    /// <summary>
    /// Answers a login that the BFF session store refused (<see cref="LoginRefusedException"/>) as a failed
    /// callback. The cookie handler calls the store in the sign-in after the OIDC events, so the refusal does not
    /// reach <c>OnRemoteFailure</c>. The response keeps its cookie deletes, such as the one of the correlation cookie.
    /// </summary>
    public static IApplicationBuilder UseLoginRefusedProblem(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (LoginRefusedException ex) when (!context.Response.HasStarted)
            {
                Logger.Here().Warning("Refused a login: {Reason}", ex.Message);
                await WriteLoginFailedAsync(context);
            }
        });

    private static Task WriteLoginFailedAsync(HttpContext context) =>
        ManagementProblem.RefuseAsync(context, AuthFailurePage.SignIn, CallbackRejectedException.Title, CallbackField,
            "The login did not complete. Start a new login.");

    public const string CallbackField = "callback";
}
