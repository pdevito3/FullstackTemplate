namespace FullstackTemplate.Server.Services;

using Microsoft.Extensions.Options;

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

public sealed class TenantScopeProvider(
    ICurrentUserService currentUserService,
    ITenantIdProvider tenantIdProvider,
    IOptions<TenancyOptions> tenancyOptions,
    SystemTenantAccess systemTenantAccess) : ITenantScopeProvider
{
    public TenantScope GetCurrentScope()
    {
        // System work opts in with CreateSystemScope. The opt-in is explicit, so it wins over the caller.
        if (systemTenantAccess.IsGranted)
            return TenantScope.AllTenants;

        var userIdentifier = currentUserService.UserIdentifier;
        if (userIdentifier != null)
        {
            // EF Core evaluates the query filter parameters synchronously, so this blocks on the async lookup.
            // AppDbContext calls this method once and keeps the scope. FusionCache usually has the lookup in
            // memory. On a cache miss, the thread waits for one database query.
            var tenantId = tenantIdProvider.GetTenantIdAsync(userIdentifier).GetAwaiter().GetResult();
            return tenantId is { } id ? TenantScope.ForTenant(id) : TenantScope.NoTenant;
        }

        if (IsMachineClientIn(tenancyOptions.Value.ReadAllTenantsClientIds)
            || IsMachineClientIn(tenancyOptions.Value.ManageAllTenantsClientIds))
            return TenantScope.AllTenants;

        // Any other caller sees no rows. This includes work with no HTTP request that did not opt in,
        // for example fire-and-forget work that lost its request.
        return TenantScope.NoTenant;
    }

    public bool CanManageAllTenants() =>
        systemTenantAccess.IsGranted || IsMachineClientIn(tenancyOptions.Value.ManageAllTenantsClientIds);

    // The issuer can give a token with no "sub" to any client that it knows, so a missing "sub" alone
    // does not make a caller trusted. Only a machine client on an allow-list is trusted.
    private bool IsMachineClientIn(string[] allowList) =>
        currentUserService.IsMachine && allowList.Contains(currentUserService.ClientId, StringComparer.Ordinal);
}
