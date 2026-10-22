using FullstackTemplate.Bff.Management;
using Microsoft.AspNetCore.Authentication;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Login;

/// <summary>
/// A callback to <c>/signin-oidc</c> that failed one of the BFF checks: the <c>iss</c> check or the strict
/// audience check. The BFF does not trust the response, and answers 400.
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
    public static async Task WriteProblemAsync(RemoteFailureContext context)
    {
        context.HandleResponse();

        if (context.Failure is CallbackRejectedException rejection)
        {
            Logger.Here().Warning("Rejected an OIDC callback: {Reason}", rejection.Message);
            await ManagementProblem.WriteAsync(context.HttpContext, CallbackRejectedException.Title, rejection.Field, rejection.Message);
            return;
        }

        // The messages of the OIDC handler name the failed step. They hold no token, code, or state value.
        Logger.Here().Warning("An OIDC callback failed: {FailureType}: {Reason}",
            context.Failure?.GetType().Name, context.Failure?.Message);
        await ManagementProblem.WriteAsync(context.HttpContext, CallbackRejectedException.Title, CallbackField,
            "The login did not complete. Start a new login.");
    }

    public const string CallbackField = "callback";
}
