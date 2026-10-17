using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FullstackTemplate.Bff;

/// <summary>
/// Centralized telemetry for easy access to tracing and metrics.
/// Usage:
///   using var activity = Telemetry.Source.StartActivity("operation-name");
///   activity?.SetTag("key", "value");
///   Telemetry.Requests.Add(1, new("endpoint", "/api/weather"));
/// </summary>
public static class Telemetry
{
    public const string ServiceName = "FullstackTemplate.Bff";

    public static readonly ActivitySource Source = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    // Common counters
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        "app.requests",
        unit: "{request}",
        description: "Number of requests processed");

    public static readonly Counter<long> Errors = Meter.CreateCounter<long>(
        "app.errors",
        unit: "{error}",
        description: "Number of errors encountered");

    public static readonly Counter<long> ProxiedRequests = Meter.CreateCounter<long>(
        "app.proxied_requests",
        unit: "{request}",
        description: "Number of requests proxied to backend");

    // Common histograms
    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "app.request.duration",
        unit: "ms",
        description: "Request processing duration");

    /// <summary>
    /// Start an activity with common tags pre-populated.
    /// </summary>
    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return Source.StartActivity(name, kind);
    }

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
