namespace FullstackTemplate.Server.Services;

using Databases;
using Microsoft.EntityFrameworkCore;
using Resources;
using Resources.Extensions;
using ZiggyCreatures.Caching.Fusion;

/// <summary>The live user record of a <c>sub</c>, in a live tenant.</summary>
public sealed record LiveUser(string UserId, string TenantId);

/// <summary>
/// Provides tenant ID lookup with caching.
/// Uses FusionCache in memory only, with no distributed cache and no backplane. An invalidation
/// applies only to the server instance that makes it.
/// </summary>
public interface ITenantIdProvider
{
    /// <summary>
    /// The live user record of the identifier and its tenant, or null. The one cached query gives both IDs, so the
    /// audit log names the user with no query of its own.
    /// </summary>
    Task<LiveUser?> FindLiveUserAsync(string userIdentifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached tenant ID for a user. <see cref="TenantLookupInterceptor"/> calls it after each save that
    /// makes a lookup stale (see <see cref="TenantLookupChange"/>). Call it yourself only after a write that bypasses the
    /// change tracker, when a user or the tenant of a user changes.
    /// </summary>
    Task InvalidateCacheAsync(string userIdentifier, CancellationToken cancellationToken = default);
}

public sealed class TenantIdProvider(
    IFusionCache cache,
    IServiceScopeFactory scopeFactory) : ITenantIdProvider, ISingletonService
{
    private static readonly FusionCacheEntryOptions CacheOptions = new()
    {
        Duration = TimeSpan.FromMinutes(30),
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(2),
        FailSafeThrottleDuration = TimeSpan.FromSeconds(30)
    };

    private static readonly TimeSpan MissingUserDuration = TimeSpan.FromSeconds(30);

    // A lookup that starts before a change is saved can finish after the invalidation and write its stale
    // result. Each invalidation moves the identifier to a new generation, and the generation is part of the
    // key. A late write then goes to the old key, which no reader uses. The generations are in buckets, so
    // the memory stays fixed. Other identifiers in the same bucket only do one more lookup.
    private const int GenerationBuckets = 4096;
    private readonly long[] _generations = new long[GenerationBuckets];

    private ref long Generation(string userIdentifier)
        => ref _generations[(uint)StringComparer.Ordinal.GetHashCode(userIdentifier) % GenerationBuckets];

    private string GetCacheKey(string userIdentifier)
        => $"tenant:user:{userIdentifier}:{Volatile.Read(ref Generation(userIdentifier))}";

    public async Task<LiveUser?> FindLiveUserAsync(string userIdentifier, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(userIdentifier);

        return await cache.GetOrSetAsync<LiveUser?>(
            cacheKey,
            async (ctx, ct) =>
            {
                var liveUser = await LookupLiveUserAsync(userIdentifier, ct);

                // Fail-safe is off, so a stale miss never outlives this duration.
                if (liveUser is null)
                {
                    ctx.Options.Duration = MissingUserDuration;
                    ctx.Options.IsFailSafeEnabled = false;
                }

                return liveUser;
            },
            CacheOptions,
            cancellationToken);
    }

    private async Task<LiveUser?> LookupLiveUserAsync(string userIdentifier, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The tenant filter reads the result of this lookup, so the lookup ignores it. IgnoreQueryFilters applies
        // to the whole query, so this includes the joined tenants. The soft-delete filter still applies to the
        // users and to the tenants, so a deleted user or a deleted tenant gives no tenant.
        return await dbContext.Users
            .TagWithOperationCallSite("users.tenant_id_lookup")
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(u => u.Identifier == userIdentifier)
            .Join(dbContext.Tenants, u => u.TenantId, t => t.Id, (u, t) => new LiveUser(u.Id, t.Id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task InvalidateCacheAsync(string userIdentifier, CancellationToken cancellationToken = default)
    {
        var staleKey = GetCacheKey(userIdentifier);
        Interlocked.Increment(ref Generation(userIdentifier));
        await cache.RemoveAsync(staleKey, token: cancellationToken);
    }
}
