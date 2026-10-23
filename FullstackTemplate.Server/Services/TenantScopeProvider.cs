namespace FullstackTemplate.Server.Services;

/// <summary>
/// Finds the tenants that the current caller sees, and what the caller can do across tenants.
/// </summary>
public interface ITenantScopeProvider
{
    /// <summary>
    /// The tenants whose rows the caller sees. The tenant query filter reads it.
    /// </summary>
    TenantScope GetCurrentScope();

    /// <summary>
    /// True if the caller can act outside one tenant, for example add a tenant or start a tenant for
    /// another user. Use this for an access decision, not <see cref="TenantScope.IncludesAllTenants"/>.
    /// </summary>
    bool CanManageAllTenants();
}

/// <summary>
/// Reads both answers from the identity of the caller. A system scope wins over the caller, and any caller that
/// <see cref="ICallerIdentityResolver"/> does not place in a tenant sees no rows. That includes work with no HTTP
/// request that did not opt in, for example fire-and-forget work that lost its request.
/// </summary>
public sealed class TenantScopeProvider(ICallerIdentityResolver callerIdentityResolver) : ITenantScopeProvider
{
    // EF Core reads the query filter parameters synchronously, so this blocks on the async identity lookups.
    // AppDbContext calls this method once and keeps the scope. FusionCache usually has the tenant of a user in memory.
    public TenantScope GetCurrentScope() => callerIdentityResolver.Current.Scope;

    public bool CanManageAllTenants() => callerIdentityResolver.Current.CanManageAllTenants;
}
