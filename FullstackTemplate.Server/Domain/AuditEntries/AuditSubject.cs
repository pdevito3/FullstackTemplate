namespace FullstackTemplate.Server.Domain.AuditEntries;

using Tenants;

/// <summary>
/// The entity that an audit entry is about, its aggregate root, and the tenant of that root. The type, the id, and
/// the tenant come from the entity, never from the caller. An action is about an aggregate root: make its subject
/// with <see cref="Of(Tenant)"/> or <see cref="Of{TEntity}(TEntity)"/>.
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

    public static AuditSubject Of(Tenant tenant) => new(nameof(Tenant), tenant.Id, nameof(Tenant), tenant.Id, tenant.Id);

    /// <summary>The subject of an action about an aggregate root that has a tenant of its own, for example a user.</summary>
    public static AuditSubject Of<TEntity>(TEntity entity) where TEntity : BaseEntity, ITenantable
    {
        var type = entity.GetType().Name;
        return new AuditSubject(type, entity.Id, type, entity.Id, entity.TenantId);
    }

    public string EntityType { get; }
    public string EntityId { get; }
    public string RootType { get; }
    public string RootId { get; }
    public string TenantId { get; }
}
