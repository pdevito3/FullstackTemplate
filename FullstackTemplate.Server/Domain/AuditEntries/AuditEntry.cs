namespace FullstackTemplate.Server.Domain.AuditEntries;

using Databases.Auditing;

/// <summary>
/// One data change of one entity, or one action, in one save. Only the save writes it, and the table refuses
/// UPDATE, DELETE, and TRUNCATE. The facts of the save (record id, time, actor, request, trace) repeat on each row
/// of the save. It is not a <see cref="BaseEntity"/>, so the save does not stamp it or capture it.
/// <see cref="ITenantable"/> gives it the tenant filter.
/// </summary>
public sealed class AuditEntry : ITenantable
{
    private AuditEntry() { } // EF Core

    internal AuditEntry(SaveFacts save, int ordinal, AuditSubject subject, AuditEntryKind kind, string? action,
        string data)
    {
        RecordId = save.RecordId;
        OccurredAt = save.OccurredAt;
        ActorUser = save.ActorUser;
        ActorClient = save.ActorClient;
        RequestName = save.RequestName;
        TraceId = save.TraceId;
        Ordinal = ordinal;
        TenantId = subject.TenantId;
        RootType = subject.RootType;
        RootId = subject.RootId;
        EntityType = subject.EntityType;
        EntityId = subject.EntityId;
        Kind = kind;
        Action = action;
        Data = data;
    }

    /// <summary>A UUIDv7 made from <see cref="OccurredAt"/>. All rows of one save share it.</summary>
    public Guid RecordId { get; private set; }

    /// <summary>The position of the row in its save, from 0.</summary>
    public int Ordinal { get; private set; }

    /// <summary>The one clock reading of the save. The stamps of the entities use the same value.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>The <c>sub</c> of the token. Null for system work.</summary>
    public string? ActorUser { get; private set; }

    /// <summary>The OAuth client of the token. Null for system work.</summary>
    public string? ActorClient { get; private set; }

    /// <summary>The MediatR request of the save, for example "UpdateUser.Command". Null when no request ran.</summary>
    public string? RequestName { get; private set; }

    /// <summary>The W3C trace id. It joins the save to its trace.</summary>
    public string? TraceId { get; private set; }

    /// <summary>The tenant of the aggregate root, never the tenant scope of the caller.</summary>
    public string TenantId { get; private set; } = null!;

    /// <summary>
    /// The aggregate root: the entity itself, or its owner at the end of its Cascade foreign keys. So the history of a
    /// user shows the changes of its permissions.
    /// </summary>
    public string RootType { get; private set; } = null!;

    public string RootId { get; private set; } = null!;

    /// <summary>The CLR name of the entity type, for example "User".</summary>
    public string EntityType { get; private set; } = null!;

    public string EntityId { get; private set; } = null!;

    public AuditEntryKind Kind { get; private set; }

    /// <summary>The name of the action. Set only when <see cref="Kind"/> is <see cref="AuditEntryKind.Action"/>.</summary>
    public string? Action { get; private set; }

    /// <summary>jsonb in the format of <see cref="AuditValues"/>. It holds personal data in full.</summary>
    public string Data { get; private set; } = null!;
}

/// <summary>Deleted and Restored are the soft delete: <c>IsDeleted</c> went from false to true, or back.</summary>
public enum AuditEntryKind
{
    Created,
    Updated,
    Deleted,
    Restored,
    Action,
}
