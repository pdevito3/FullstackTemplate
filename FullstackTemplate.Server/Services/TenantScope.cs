namespace FullstackTemplate.Server.Services;

using Domain;
using Domain.Tenants;

/// <summary>
/// The tenants whose rows the current request can see. The tenant query filter reads it.
/// There are three scopes: all tenants, one tenant, and no tenant.
/// </summary>
public sealed record TenantScope
{
    /// <summary>
    /// The rows of all tenants. For system work in a scope from <see cref="SystemScopeExtensions.CreateSystemScope"/>,
    /// and for a machine client on the allow-list.
    /// </summary>
    public static TenantScope AllTenants { get; } = new(includesAllTenants: true, tenantId: null);

    /// <summary>
    /// No rows. For a caller that does not belong to a tenant.
    /// </summary>
    public static TenantScope NoTenant { get; } = new(includesAllTenants: false, tenantId: null);

    /// <summary>
    /// The rows of one tenant.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is not a valid tenant ID.</exception>
    public static TenantScope ForTenant(string tenantId)
    {
        if (!EntityId.IsValid(tenantId, Tenant.IdPrefix))
            throw new ArgumentException($"A tenant scope needs a valid tenant ID, not '{tenantId}'.", nameof(tenantId));

        return new TenantScope(includesAllTenants: false, tenantId);
    }

    private TenantScope(bool includesAllTenants, string? tenantId)
    {
        IncludesAllTenants = includesAllTenants;
        TenantId = tenantId;
    }

    public bool IncludesAllTenants { get; }

    /// <summary>
    /// The tenant of a one-tenant scope. Null for the other scopes.
    /// </summary>
    public string? TenantId { get; }

    /// <summary>
    /// True when the scope includes <paramref name="tenantId"/>. A null ID matches only the all-tenants scope.
    /// </summary>
    public bool Includes(string? tenantId) => IncludesAllTenants || (TenantId is not null && TenantId == tenantId);
}
