namespace FullstackTemplate.Server.Domain;

/// <summary>
/// Marker interface for entities that are tenant-scoped.
/// Entities implementing this interface will be automatically filtered by tenant.
/// </summary>
public interface ITenantable
{
    Guid TenantId { get; }
}
