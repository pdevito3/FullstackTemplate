namespace FullstackTemplate.Server.Databases.Auditing;

using Domain.AuditEntries;

/// <summary>
/// One audited entity of the tracker after the stamps, as plain values. <see cref="AuditSnapshots"/> makes it.
/// It holds only the recorded values of the type. See <see cref="AuditModel"/>.
/// </summary>
internal sealed record EntitySnapshot(
    AuditSubject Subject,
    bool IsAdded,
    bool WasDeleted,
    bool IsDeleted,
    IReadOnlyList<PropertySnapshot> Properties);

/// <param name="Path">The EF property name, for example "FirstName". A complex property with one value, which is
/// each <c>ValueObject</c>, uses its own name ("Email"). A complex property with more values uses "Address.City".</param>
/// <param name="Original">The provider value before the save. Null for an added entity.</param>
/// <param name="Current">The provider value that the save writes.</param>
internal readonly record struct PropertySnapshot(string Path, object? Original, object? Current);

internal sealed record DataChange(AuditSubject Subject, AuditEntryKind Kind, IReadOnlyList<PropertySnapshot> Properties);

/// <summary>The facts of one save that each of its audit entries shares.</summary>
internal sealed record SaveFacts(
    Guid RecordId,
    DateTimeOffset OccurredAt,
    string? ActorUser,
    string? ActorClient,
    string? RequestName,
    string? TraceId)
{
    /// <summary>A blank actor becomes null, so "no caller" has one form.</summary>
    public static SaveFacts From(DateTimeOffset now, string? actorUser, string? actorClient, string? requestName,
        string? traceId)
        => new(Guid.CreateVersion7(now), now, NullIfBlank(actorUser), NullIfBlank(actorClient), requestName, traceId);

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

internal static class AuditDiff
{
    /// <summary>
    /// Decides what each snapshot changed. It compares original and current values, never <c>IsModified</c>, because a
    /// soft delete marks every property modified. An update that changed no recorded value gives no change. That drops
    /// the owner touch, which writes only <c>LastModifiedOn</c>.
    /// </summary>
    public static IReadOnlyList<DataChange> Changes(IEnumerable<EntitySnapshot> snapshots)
    {
        var changes = new List<DataChange>();
        foreach (var snapshot in snapshots)
        {
            if (snapshot.IsAdded)
            {
                changes.Add(new DataChange(snapshot.Subject, AuditEntryKind.Created,
                    snapshot.Properties.Where(p => p.Current is not null).ToList()));
                continue;
            }

            var changed = snapshot.Properties.Where(p => !Equals(p.Original, p.Current)).ToList();
            var kind = (snapshot.WasDeleted, snapshot.IsDeleted) switch
            {
                (false, true) => AuditEntryKind.Deleted,
                (true, false) => AuditEntryKind.Restored,
                _ => AuditEntryKind.Updated,
            };
            if (kind == AuditEntryKind.Updated && changed.Count == 0)
                continue;

            changes.Add(new DataChange(snapshot.Subject, kind, changed));
        }

        return changes;
    }

    /// <summary>The audit entries of one save, numbered in the order of <paramref name="changes"/>.</summary>
    public static IReadOnlyList<AuditEntry> Compose(SaveFacts save, IReadOnlyList<DataChange> changes)
        => changes
            .Select((change, ordinal) => new AuditEntry(save, ordinal, change.Subject, change.Kind, action: null,
                AuditValues.WriteChange(change.Kind, change.Properties)))
            .ToList();
}
