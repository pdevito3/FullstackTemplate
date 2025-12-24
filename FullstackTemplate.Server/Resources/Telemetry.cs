namespace FullstackTemplate.Server.Resources;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Centralized telemetry for easy access to tracing and metrics.
/// Usage:
///   using var activity = Telemetry.Source.StartActivity("operation-name");
///   activity?.SetTag("key", "value");
///   Telemetry.Requests.Add(1, new("endpoint", "/api/weather"));
/// </summary>
public static class Telemetry
{
    public const string ServiceName = "FullstackTemplate.Server";

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
}
