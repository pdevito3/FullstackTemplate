namespace FullstackTemplate.Server.Domain.Attachments;

using System.Diagnostics.Metrics;
using Resources;

public static class AttachmentMetrics
{
    public static readonly Histogram<long> Added = Telemetry.Meter.CreateHistogram<long>(
        "app.attachments.added",
        unit: "By",
        description: "Size of each attachment that was added, tagged with the entity type. The count is the number of uploads");

    public static readonly Counter<long> Deleted = Telemetry.Meter.CreateCounter<long>(
        "app.attachments.deleted",
        unit: "{attachment}",
        description: "Number of attachments that were deleted, tagged with the entity type");

    public static void RecordAdded<TAttachment>(long sizeBytes) where TAttachment : IAttachment
        => Added.Record(sizeBytes, EntityTypeTag<TAttachment>());

    public static void RecordDeleted<TAttachment>() where TAttachment : IAttachment
        => Deleted.Add(1, EntityTypeTag<TAttachment>());

    private static KeyValuePair<string, object?> EntityTypeTag<TAttachment>()
        => new("app.attachment.entity_type", typeof(TAttachment).Name);
}
