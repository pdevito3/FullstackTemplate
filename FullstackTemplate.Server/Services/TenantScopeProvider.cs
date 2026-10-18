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
            // The query filter reads the scope synchronously. FusionCache keeps the lookup in memory.
            var tenantId = tenantIdProvider.GetTenantIdAsync(userIdentifier).GetAwaiter().GetResult();
            return tenantId is { } id ? TenantScope.ForTenant(id) : TenantScope.NoTenant;
        }

        if (IsAllowListedMachineClient())
            return TenantScope.AllTenants;

        // Any other caller sees no rows. This includes work with no HTTP request that did not opt in,
        // for example fire-and-forget work that lost its request.
        return TenantScope.NoTenant;
    }

    public bool CanManageAllTenants() => systemTenantAccess.IsGranted || IsAllowListedMachineClient();

    // The issuer can give a token with no "sub" to any client that it knows, so a missing "sub" alone
    // does not make a caller trusted. Only a machine client on the allow-list is trusted.
    private bool IsAllowListedMachineClient() =>
        currentUserService.IsMachine
        && tenancyOptions.Value.AllTenantClientIds.Contains(currentUserService.ClientId, StringComparer.Ordinal);
}
