namespace FullstackTemplate.Server.Domain.ObjectDeleteRequests;

using System.Diagnostics.Metrics;
using Resources;

public static class ObjectDeleteMetrics
{
    public static readonly Counter<long> Attempted = Telemetry.Meter.CreateCounter<long>(
        "app.object_deletes.attempted",
        unit: "{attempt}",
        description: "Attempts of the object delete requests, tagged with the outcome: deleted, retry_later, dead, or lost_claim. A dead request needs a person");

    public static void Record(ObjectDeleteOutcome outcome)
        => Attempted.Add(1, new KeyValuePair<string, object?>("app.object_delete.outcome", NameOf(outcome)));

    public static string NameOf(ObjectDeleteOutcome outcome) => outcome switch
    {
        ObjectDeleteOutcome.Deleted => "deleted",
        ObjectDeleteOutcome.RetryLater => "retry_later",
        ObjectDeleteOutcome.Dead => "dead",
        ObjectDeleteOutcome.LostClaim => "lost_claim",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
