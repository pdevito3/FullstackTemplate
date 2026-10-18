namespace FullstackTemplate.Server.Services;

using Microsoft.Extensions.Options;

/// <summary>
/// Finds the tenant scope of the current request.
/// </summary>
public interface ITenantScopeProvider
{
    TenantScope GetCurrentScope();
}

public sealed class TenantScopeProvider(
    ICurrentUserService currentUserService,
    ITenantIdProvider tenantIdProvider,
    IOptions<TenancyOptions> tenancyOptions) : ITenantScopeProvider
{
    public TenantScope GetCurrentScope()
    {
        var userIdentifier = currentUserService.UserIdentifier;
        if (userIdentifier != null)
        {
            // The query filter reads the scope synchronously. FusionCache keeps the lookup in memory.
            var tenantId = tenantIdProvider.GetTenantIdAsync(userIdentifier).GetAwaiter().GetResult();
            return tenantId is { } id ? TenantScope.ForTenant(id) : TenantScope.NoTenant;
        }

        // No HTTP request, so no caller: startup work, background work, or the REPL.
        if (currentUserService.User is null)
            return TenantScope.AllTenants;

        // The issuer can give a token with no "sub" to any client that it knows, so a missing "sub" alone
        // does not make a caller trusted. Only a machine client on the allow-list sees all tenants.
        if (currentUserService.IsMachine
            && tenancyOptions.Value.AllTenantClientIds.Contains(currentUserService.ClientId, StringComparer.Ordinal))
            return TenantScope.AllTenants;

        return TenantScope.NoTenant;
    }
}
