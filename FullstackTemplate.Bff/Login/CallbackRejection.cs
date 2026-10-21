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
    /// The <c>OnRemoteFailure</c> event: a failure of a BFF check becomes a 400 problem response. Other failures
    /// keep the handler default.
    /// </summary>
    public static async Task WriteProblemAsync(RemoteFailureContext context)
    {
        if (context.Failure is not CallbackRejectedException rejection)
            return;

        Logger.Here().Warning("Rejected an OIDC callback: {Reason}", rejection.Message);
        context.HandleResponse();
        await ManagementProblem.WriteAsync(context.HttpContext, CallbackRejectedException.Title, rejection.Field, rejection.Message);
    }
}
