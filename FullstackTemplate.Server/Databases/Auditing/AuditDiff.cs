namespace FullstackTemplate.Server.Databases.Auditing;

using Domain.AuditEntries;
using Services;

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

/// <summary>Who made a save.</summary>
/// <param name="Subject">The <c>sub</c> of the token. A provider can make it an email address, so the read shows it
/// only with <c>read_audit_log_sensitive</c>.</param>
/// <param name="ClientId">The client of the token.</param>
/// <param name="UserId">The user record of a user, or of the user that a delegated agent acts for. Null when the
/// user has no live user record yet, for example when the user starts a tenant.</param>
/// <param name="AgentId">The agent record of a delegated or a machine agent.</param>
internal sealed record AuditActor(string? Subject, string? ClientId, string? UserId, string? AgentId)
{
    /// <summary>A blank claim becomes null, so "no caller" has one form.</summary>
    public static AuditActor Of(string? subject, string? clientId, CallerIdentity caller) => new(
        NullIfBlank(subject),
        NullIfBlank(clientId),
        caller switch
        {
            CallerIdentity.User { LiveUser: { } liveUser } => liveUser.UserId,
            CallerIdentity.DelegatedAgent agent => agent.LiveUser.UserId,
            _ => null,
        },
        caller switch
        {
            CallerIdentity.DelegatedAgent agent => agent.AgentId,
            CallerIdentity.MachineAgent agent => agent.AgentId,
            _ => null,
        });

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>The facts of one save that each of its audit entries shares.</summary>
internal sealed record SaveFacts(
    Guid RecordId,
    DateTimeOffset OccurredAt,
    AuditActor Actor,
    string? RequestName,
    string? TraceId)
{
    public static SaveFacts From(DateTimeOffset now, AuditActor actor, string? requestName, string? traceId)
        => new(Guid.CreateVersion7(now), now, actor, requestName, traceId);
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

            var kind = (snapshot.WasDeleted, snapshot.IsDeleted) switch
            {
                (false, true) => AuditEntryKind.Deleted,
                (true, false) => AuditEntryKind.Restored,
                _ => AuditEntryKind.Updated,
            };
            // A delete keeps every value that the row held, so the entry says what was deleted.
            var recorded = kind == AuditEntryKind.Deleted
                ? snapshot.Properties.Where(p => p.Original is not null || p.Current is not null).ToList()
                : snapshot.Properties.Where(p => !Equals(p.Original, p.Current)).ToList();
            if (kind == AuditEntryKind.Updated && recorded.Count == 0)
                continue;

            changes.Add(new DataChange(snapshot.Subject, kind, recorded));
        }

        return changes;
    }

    /// <summary>
    /// The audit entries of one save, numbered in order: the <paramref name="changes"/>, then the
    /// <paramref name="actions"/> in the order that the handlers recorded them.
    /// </summary>
    public static IReadOnlyList<AuditEntry> Compose(SaveFacts save, IReadOnlyList<DataChange> changes,
        IReadOnlyList<AuditAction> actions)
        => changes
            .Select(change => (change.Subject, change.Kind, Action: (string?)null,
                Data: AuditValues.WriteChange(change.Kind, change.Properties)))
            .Concat(actions.Select(action => (action.Subject, Kind: AuditEntryKind.Action,
                Action: (string?)AuditAction.NameOf(action.GetType()), Data: AuditValues.WriteAction(action))))
            .Select((entry, ordinal) => new AuditEntry(save, ordinal, entry.Subject, entry.Kind, entry.Action, entry.Data))
            .ToList();
}
