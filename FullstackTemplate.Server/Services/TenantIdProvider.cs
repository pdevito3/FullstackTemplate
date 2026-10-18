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

    private static readonly TimeSpan MissingUserDuration = TimeSpan.FromSeconds(30);

    private string GetCacheKey(string userIdentifier) => $"tenant:user:{userIdentifier}";
    
    public async Task<Guid?> GetTenantIdAsync(string userIdentifier, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(userIdentifier);

        return await cache.GetOrSetAsync<Guid?>(
            cacheKey,
            async (ctx, ct) =>
            {
                var tenantId = await LookupTenantIdAsync(userIdentifier, ct);

                // Cache a missing user for a short time only. Without this, each query from a signed-in user
                // with no user record runs this lookup again. UserCreatedHandler removes the key when the user
                // is added, so the short duration only limits a stale miss from some other write path.
                // Fail-safe is off, so a stale miss never outlives this duration.
                if (tenantId is null)
                {
                    ctx.Options.Duration = MissingUserDuration;
                    ctx.Options.IsFailSafeEnabled = false;
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
