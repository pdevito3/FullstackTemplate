namespace FullstackTemplate.Server.Services;

/// <summary>
/// Finds the tenants that the current caller sees. It does not tell what the caller can do. For an access decision, ask
/// <see cref="Domain.Users.Caller"/> through <see cref="Domain.Users.IPermissionGuard"/>.
/// </summary>
public interface ITenantScopeProvider
{
    /// <summary>
    /// The tenants whose rows the caller sees. The tenant query filter reads it.
    /// </summary>
    TenantScope GetCurrentScope();
}

/// <summary>
/// Reads the scope from the identity of the caller. A system scope wins over the caller, and any caller that
/// <see cref="ICallerIdentityResolver"/> does not place in a tenant sees no rows. That includes work with no HTTP
/// request that did not opt in, for example fire-and-forget work that lost its request.
/// </summary>
public sealed class TenantScopeProvider(ICallerIdentityResolver callerIdentityResolver) : ITenantScopeProvider
{
    // EF Core reads the query filter parameters synchronously, so this blocks on the async identity lookups.
    // AppDbContext calls this method once and keeps the scope. FusionCache usually has the tenant of a user in memory.
    public TenantScope GetCurrentScope() => callerIdentityResolver.Current.Scope;
}
