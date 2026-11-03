namespace FullstackTemplate.Server.Examples.Workshops;

using System.Diagnostics.Metrics;
using Resources;

public static class WorkshopMetrics
{
    public static readonly Counter<long> StatusChanged = Telemetry.Meter.CreateCounter<long>(
        "app.workshops.status_changed",
        unit: "{workshop}",
        description: "Number of workshop status changes, tagged with the new status");

    public static readonly Histogram<long> AttachmentAdded = Telemetry.Meter.CreateHistogram<long>(
        "app.workshop_attachments.added",
        unit: "By",
        description: "Size of each workshop attachment that was added. The count is the number of uploads");

    public static readonly Counter<long> AttachmentDeleted = Telemetry.Meter.CreateCounter<long>(
        "app.workshop_attachments.deleted",
        unit: "{attachment}",
        description: "Number of workshop attachments that were deleted");

    public static void RecordStatusChange(WorkshopStatus status)
        => StatusChanged.Add(1, new KeyValuePair<string, object?>("app.workshop.status", status.Value));

    public static void RecordAttachmentAdded(long sizeBytes) => AttachmentAdded.Record(sizeBytes);

    public static void RecordAttachmentDeleted() => AttachmentDeleted.Add(1);
}
