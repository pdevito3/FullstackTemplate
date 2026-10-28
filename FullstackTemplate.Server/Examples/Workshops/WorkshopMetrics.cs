namespace FullstackTemplate.Server.Examples.Workshops;

using System.Diagnostics.Metrics;
using Resources;

public static class WorkshopMetrics
{
    public static readonly Counter<long> StatusChanged = Telemetry.Meter.CreateCounter<long>(
        "app.workshops.status_changed",
        unit: "{workshop}",
        description: "Number of workshop status changes, tagged with the new status");

    public static void RecordStatusChange(WorkshopStatus status)
        => StatusChanged.Add(1, new KeyValuePair<string, object?>("app.workshop.status", status.Value));
}
