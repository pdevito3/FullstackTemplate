namespace FullstackTemplate.FunctionalTests.TestUtilities;

using System.Collections.Concurrent;
using FullstackTemplate.Server.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Fails each save that writes an entity whose ID a test armed. All tests share one app, so the ID keeps the
/// failure inside the test that armed it.
/// </summary>
public sealed class FailingSaves : SaveChangesInterceptor
{
    private readonly ConcurrentDictionary<string, byte> _armedIds = new();

    public void Arm(string entityId) => _armedIds.TryAdd(entityId, 0);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        FailWhenArmed(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        FailWhenArmed(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void FailWhenArmed(DbContext? context)
    {
        var writesArmedEntity = context?.ChangeTracker.Entries<BaseEntity>()
            .Any(e => e.State is not (EntityState.Unchanged or EntityState.Detached) && _armedIds.ContainsKey(e.Entity.Id));
        if (writesArmedEntity == true)
            throw new InvalidOperationException("The test failed this save.");
    }
}
