namespace FullstackTemplate.Server.Services;

/// <summary>
/// Finds the tenant scope of the current request.
/// </summary>
public interface ITenantScopeProvider
{
    TenantScope GetCurrentScope();
}

public sealed class TenantScopeProvider(
    ICurrentUserService currentUserService,
    ITenantIdProvider tenantIdProvider) : ITenantScopeProvider
{
    public TenantScope GetCurrentScope()
    {
        var userIdentifier = currentUserService.UserIdentifier;
        if (userIdentifier == null)
            return TenantScope.AllTenants;

        // The query filter reads the scope synchronously. FusionCache keeps the lookup in memory.
        var tenantId = tenantIdProvider.GetTenantIdAsync(userIdentifier).GetAwaiter().GetResult();
        return tenantId is { } id ? TenantScope.ForTenant(id) : TenantScope.NoTenant;
    }
}
