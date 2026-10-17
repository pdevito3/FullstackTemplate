namespace FullstackTemplate.Server.Resources;

using System.Diagnostics;

/// <summary>
/// Holds a root <see cref="Activity"/> together with the ambient activity it displaced, and puts that
/// ambient activity back on dispose. <see cref="Telemetry.TraceRoot"/> returns this scope, so a
/// <c>using</c> block both ends the root span and restores the caller's trace in one step.
/// </summary>
public sealed class ActivityScope(Activity? activity, Activity? previousActivity) : IDisposable
{
    /// <summary>The started root span, or null when no listener sampled it.</summary>
    public Activity? Activity { get; } = activity;

    public void Dispose()
    {
        Activity?.Dispose();
        System.Diagnostics.Activity.Current = previousActivity;
    }
}
