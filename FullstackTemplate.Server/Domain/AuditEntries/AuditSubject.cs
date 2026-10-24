namespace FullstackTemplate.Server.Domain.AuditEntries;

/// <summary>
/// The entity that an audit entry is about, its aggregate root, and the tenant of that root. The type, the id, and
/// the tenant come from the tracked entity, never from the caller.
/// </summary>
public sealed record AuditSubject
{
    internal AuditSubject(string entityType, string entityId, string rootType, string rootId, string tenantId)
    {
        EntityType = entityType;
        EntityId = entityId;
        RootType = rootType;
        RootId = rootId;
        TenantId = tenantId;
    }

    public string EntityType { get; }
    public string EntityId { get; }
    public string RootType { get; }
    public string RootId { get; }
    public string TenantId { get; }
}
