namespace FullstackTemplate.Server.Services;

using Databases;
using Microsoft.EntityFrameworkCore;
using Resources;
using ZiggyCreatures.Caching.Fusion;

/// <summary>
/// Provides tenant ID lookup with caching.
/// Uses FusionCache for in-memory caching with support for Redis as distributed L2 cache.
/// </summary>
public interface ITenantIdProvider
{
    /// <summary>
    /// Gets the tenant ID for a user by their identifier.
    /// </summary>
    Task<Guid?> GetTenantIdAsync(string userIdentifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached tenant ID for a user.
    /// Call this when a user's tenant assignment changes.
    /// </summary>
    Task InvalidateCacheAsync(string userIdentifier, CancellationToken cancellationToken = default);
}

public sealed class TenantIdProvider(
    IFusionCache cache,
    IServiceScopeFactory scopeFactory) : ITenantIdProvider
{
    private static readonly FusionCacheEntryOptions CacheOptions = new()
    {
        Duration = TimeSpan.FromMinutes(30),
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(2),
        FailSafeThrottleDuration = TimeSpan.FromSeconds(30)
    };

    private string GetCacheKey(string userIdentifier) => $"tenant:user:{userIdentifier}";
    
    public async Task<Guid?> GetTenantIdAsync(string userIdentifier, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(userIdentifier);

        return await cache.GetOrSetAsync<Guid?>(
            cacheKey,
            async (ctx, ct) =>
            {
                var tenantId = await LookupTenantIdAsync(userIdentifier, ct);

                // Do not cache a missing user. Nothing invalidates this key when the user is added later,
                // so a cached null keeps a new user out of their tenant for the full duration.
                if (tenantId is null)
                {
                    ctx.Options.SkipMemoryCacheWrite = true;
                    ctx.Options.SkipDistributedCacheWrite = true;
                }

                return tenantId;
            },
            CacheOptions,
            cancellationToken);
    }

    private async Task<Guid?> LookupTenantIdAsync(string userIdentifier, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await dbContext.Users
            .TagWithOperationCallSite("users.tenant_id_lookup")
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(u => u.Identifier == userIdentifier)
            .Select(u => (Guid?)u.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task InvalidateCacheAsync(string userIdentifier, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(userIdentifier);
        await cache.RemoveAsync(cacheKey, token: cancellationToken);
    }
}
