namespace FullstackTemplate.Server.Domain.ObjectDeleteRequests;

using Attachments;
using Resources;
using Tenants;

/// <summary>
/// Asks for the delete of objects in storage. The save that deletes the rows that name the objects adds the request,
/// so the request commits with the row delete, or neither commits. <see cref="ObjectDeleteRunner"/> then deletes the
/// objects after the commit, and tries again after a failure, in the shape of ADR 0010. It is a queue of the Server, not
/// an aggregate: it has no events, and it outlives its tenant, so a tenant delete does not delete it.
/// <see cref="ITenantable"/> gives it the tenant filter, so a query of a tenant sees only its own requests.
/// </summary>
[NotAudited("A queue of work for storage. The audit entry of the row delete that adds a request records the change.")]
public sealed class ObjectDeleteRequest : ITenantable
{
    public const string IdPrefix = "objdel";
    public const int LastErrorMaxLength = 2000;
    public const int TraceParentLength = 55;
    public const int TraceStateMaxLength = 512;

    public string Id { get; private set; } = default!;
    public string TenantId { get; private set; } = default!;

    /// <summary>The key of one object, or, when <see cref="IsPrefix"/> is true, the start of the key of each object.</summary>
    public string Key { get; private set; } = default!;

    public bool IsPrefix { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>
    /// The time after which a runner can claim the request. A claim moves it past the end of the attempt, so a runner
    /// that stops during the attempt gives the request back at that time.
    /// </summary>
    public DateTimeOffset DueAt { get; private set; }

    /// <summary>The attempts that runners claimed, also an attempt whose runner stopped before the end.</summary>
    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>The time that the request used its last attempt. No runner claims it again, and a person must look.</summary>
    public DateTimeOffset? DeadAt { get; private set; }

    /// <summary>
    /// The trace context of the save that made the request. Each attempt is a trace of its own, and its
    /// <c>object_delete.run</c> span links to this context, so a person can go from an attempt to the delete.
    /// </summary>
    public string? TraceParent { get; private set; }

    public string? TraceState { get; private set; }

    private ObjectDeleteRequest() { } // For EF Core

    /// <summary>
    /// The requests for the deleted rows of one save: one for each deleted attachment, and one for all the objects of
    /// each deleted tenant. The request of a tenant covers the attachments of the tenant and each object that no row
    /// names, so an attachment of a deleted tenant gets no request of its own.
    /// </summary>
    public static IReadOnlyList<ObjectDeleteRequest> ForDeleted(IEnumerable<object> deletedEntities, DateTimeOffset now,
        TraceContext trace)
    {
        var deleted = deletedEntities.ToList();
        var tenants = deleted.OfType<Tenant>().Select(t => t.Id).ToHashSet();
        var fitted = Fit(trace);
        return
        [
            .. tenants.Select(tenantId => New(tenantId, StoredFile.TenantPrefix(tenantId), isPrefix: true, now, fitted)),
            .. deleted.OfType<IAttachment>()
                .Where(a => !tenants.Contains(a.TenantId))
                .Select(a => New(a.TenantId, a.File.StorageKey, isPrefix: false, now, fitted)),
        ];
    }

    /// <summary>
    /// False for a claim after the last attempt. Such a claim comes after a runner stopped during the last attempt.
    /// </summary>
    public bool HasAttemptLeft(ObjectDeleteOptions options) => Attempts <= options.MaxAttempts;

    /// <summary>
    /// The state after a failed attempt: due again after a delay that doubles with each attempt, or dead after the last
    /// attempt.
    /// </summary>
    public ObjectDeleteFailure AfterFailure(string error, DateTimeOffset now, ObjectDeleteOptions options)
    {
        var lastError = error.Length <= LastErrorMaxLength ? error : error[..LastErrorMaxLength];
        if (Attempts >= options.MaxAttempts)
            return new ObjectDeleteFailure(DueAt, now, lastError);

        var delay = options.FirstRetryDelay * Math.Pow(2, Math.Max(Attempts - 1, 0));
        return new ObjectDeleteFailure(now + (delay < options.MaxRetryDelay ? delay : options.MaxRetryDelay), null, lastError);
    }

    // The trace state comes from the tracestate header of the caller, so a long one must not fail the save of the delete
    private static TraceContext? Fit(TraceContext trace)
        => trace.TraceParent is { Length: TraceParentLength } && (trace.TraceState?.Length ?? 0) <= TraceStateMaxLength
            ? trace
            : null;

    // A key from another tenant would make the tenant of the request delete the objects of that tenant
    private static ObjectDeleteRequest New(string tenantId, string key, bool isPrefix, DateTimeOffset now, TraceContext? trace)
    {
        if (!key.StartsWith(StoredFile.TenantPrefix(tenantId), StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"The key of an object delete of tenant {tenantId} must start with {StoredFile.TenantPrefix(tenantId)}.");

        return new ObjectDeleteRequest
        {
            Id = EntityId.New(IdPrefix),
            TenantId = tenantId,
            Key = key,
            IsPrefix = isPrefix,
            RequestedAt = now,
            DueAt = now,
            TraceParent = trace?.TraceParent,
            TraceState = trace?.TraceState,
        };
    }
}

/// <summary>The state of a request after a failed attempt. <see cref="DeadAt"/> is set when no attempt is left.</summary>
public sealed record ObjectDeleteFailure(DateTimeOffset DueAt, DateTimeOffset? DeadAt, string LastError)
{
    public bool IsDead => DeadAt is not null;
}
