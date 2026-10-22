using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FullstackTemplate.Bff;

/// <summary>
/// Centralized telemetry for easy access to tracing and metrics, plus a trace context that travels in
/// data instead of headers (<see cref="TraceContext"/>).
/// Usage:
///   using var activity = Telemetry.Source.StartActivity("operation-name");
///   Telemetry.RecordError(activity, exception);
/// </summary>
public static class Telemetry
{
    public const string ServiceName = "FullstackTemplate.Bff";

    public static readonly ActivitySource Source = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    public static readonly Counter<long> Errors = Meter.CreateCounter<long>(
        "app.errors",
        unit: "{error}",
        description: "Number of errors encountered");

    /// <summary>
    /// The BFF sessions that a login created. Add 1 for each new row.
    /// </summary>
    public static readonly Counter<long> SessionsStarted = Meter.CreateCounter<long>(
        "bff.sessions.started",
        unit: "{session}",
        description: "Number of BFF sessions that a login created");

    /// <summary>
    /// The BFF sessions that ended, with the <c>reason</c> tag. Use <see cref="RecordSessionsEnded"/>.
    /// </summary>
    public static readonly Counter<long> SessionsEnded = Meter.CreateCounter<long>(
        "bff.sessions.ended",
        unit: "{session}",
        description: "Number of BFF sessions that ended");

    /// <summary>
    /// The failed token refreshes, with the <c>kind</c> tag. Use <see cref="RecordRefreshFailure"/>.
    /// </summary>
    public static readonly Counter<long> RefreshFailures = Meter.CreateCounter<long>(
        "bff.refresh.failures",
        unit: "{failure}",
        description: "Number of failed token refreshes");

    /// <summary>
    /// Adds <paramref name="count"/> to <c>bff.sessions.ended</c> with the tag <c>reason</c>, one of
    /// <see cref="SessionEndReason"/>. A count of 0 records nothing.
    /// </summary>
    public static void RecordSessionsEnded(string reason, long count = 1)
    {
        if (count > 0)
            SessionsEnded.Add(count, new KeyValuePair<string, object?>("reason", reason));
    }

    /// <summary>
    /// Adds 1 to <c>bff.refresh.failures</c> with the tag <c>kind</c>, one of <see cref="RefreshFailureKind"/>.
    /// </summary>
    public static void RecordRefreshFailure(string kind) =>
        RefreshFailures.Add(1, new KeyValuePair<string, object?>("kind", kind));

    /// <summary>
    /// Record an error on the current activity.
    /// </summary>
    public static void RecordError(Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.AddException(ex);
        Errors.Add(1, new KeyValuePair<string, object?>("exception.type", ex.GetType().Name));
    }

    /// <summary>
    /// Captures the current trace context for a hop that headers cannot cover. Both values are null
    /// when no activity is current, and <see cref="TryParseTraceContext"/> then reports false.
    /// </summary>
    public static TraceContext CaptureCurrentTraceContext() =>
        new(Activity.Current?.Id, Activity.Current?.TraceStateString);

    /// <summary>
    /// Parses a captured <see cref="TraceContext"/> back into an <see cref="ActivityContext"/>.
    /// Returns false for a null, empty, or malformed context, so a caller that receives no context
    /// keeps working with an untraced parent.
    /// </summary>
    public static bool TryParseTraceContext(TraceContext? traceContext, out ActivityContext activityContext)
    {
        activityContext = default;
        if (traceContext is null || string.IsNullOrWhiteSpace(traceContext.TraceParent))
            return false;

        return ActivityContext.TryParse(
            traceContext.TraceParent, traceContext.TraceState, isRemote: true, out activityContext);
    }
}

/// <summary>
/// The values of the <c>reason</c> tag of <c>bff.sessions.ended</c>.
/// </summary>
public static class SessionEndReason
{
    /// <summary><c>/bff/logout</c> deleted the row.</summary>
    public const string Logout = "logout";

    /// <summary>A logout token on <c>/bff/backchannel</c> deleted the row.</summary>
    public const string BackChannelLogout = "backchannel_logout";

    /// <summary>A refresh with a permanent failure ended the BFF session.</summary>
    public const string RefreshFailure = "refresh_failure";

    /// <summary>The cleanup deleted the row after its expiry or its absolute limit.</summary>
    public const string Expiry = "expiry";
}

/// <summary>
/// The values of the <c>kind</c> tag of <c>bff.refresh.failures</c>.
/// </summary>
public static class RefreshFailureKind
{
    /// <summary>The identity provider answered <c>invalid_grant</c>. A retry cannot succeed.</summary>
    public const string Permanent = "permanent";

    /// <summary>Any other failure, such as a network error, a timeout, or a 5xx. A later retry can succeed.</summary>
    public const string Temporary = "temporary";
}
