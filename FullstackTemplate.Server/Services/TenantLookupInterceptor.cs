namespace FullstackTemplate.Server.Services;

using System.Runtime.CompilerServices;
using Databases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Resources;
using Serilog;

/// <summary>
/// It applies the change after a failed or canceled save too, because a lost acknowledgment can hide a commit.
/// </summary>
public sealed class TenantLookupInterceptor(ITenantIdProvider tenantIdProvider) : SaveChangesInterceptor
{
    private static readonly ILogger Logger = Log.ForContext<TenantLookupInterceptor>();

    private readonly ConditionalWeakTable<DbContext, TenantLookupChange> _changes = new();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is AppDbContext dbContext)
            _changes.AddOrUpdate(dbContext, await TenantLookupChange.CollectAsync(dbContext, cancellationToken));
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(eventData.Context);
        return result;
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        => ApplyAsync(eventData.Context);

    public override Task SaveChangesCanceledAsync(
        DbContextEventData eventData, CancellationToken cancellationToken = default)
        => ApplyAsync(eventData.Context);

    private async Task ApplyAsync(DbContext? context)
    {
        if (context is null || !_changes.TryGetValue(context, out var change))
            return;
        _changes.Remove(context);

        try
        {
            await change.ApplyAsync(tenantIdProvider, CancellationToken.None);
        }
        catch (Exception exception)
        {
            Logger.Here().Error(exception, "The removal of {StaleLookupCount} stale tenant lookups failed",
                change.StaleIdentifiers.Count);
        }
    }
}
