namespace FullstackTemplate.Server.Services;

/// <summary>
/// The tenants whose rows the current request can see. The tenant query filter reads it.
/// There are three scopes: all tenants, one tenant, and no tenant.
/// </summary>
public sealed record TenantScope
{
    /// <summary>
    /// The rows of all tenants. For system work that has no user.
    /// </summary>
    public static TenantScope AllTenants { get; } = new(includesAllTenants: true, tenantId: null);

    /// <summary>
    /// No rows. For a caller that does not belong to a tenant.
    /// </summary>
    public static TenantScope NoTenant { get; } = new(includesAllTenants: false, tenantId: null);

    /// <summary>
    /// The rows of one tenant.
    /// </summary>
    public static TenantScope ForTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant scope needs a tenant ID.", nameof(tenantId));

        return new TenantScope(includesAllTenants: false, tenantId);
    }

    private TenantScope(bool includesAllTenants, Guid? tenantId)
    {
        IncludesAllTenants = includesAllTenants;
        TenantId = tenantId;
    }

    public bool IncludesAllTenants { get; }

    /// <summary>
    /// The tenant of a one-tenant scope. Null for the other scopes.
    /// </summary>
    public Guid? TenantId { get; }

    public bool Includes(Guid tenantId) => IncludesAllTenants || TenantId == tenantId;
}
