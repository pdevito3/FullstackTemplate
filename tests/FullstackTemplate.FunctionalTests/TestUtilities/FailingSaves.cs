namespace FullstackTemplate.FunctionalTests.TestUtilities;

using System.Collections.Concurrent;
using FullstackTemplate.Server.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Fails each save that writes an entity that a test armed. All tests share one app, so the ID or the match keeps
/// the failure inside the test that armed it.
/// </summary>
public sealed class FailingSaves : SaveChangesInterceptor
{
    private readonly ConcurrentDictionary<string, byte> _armedIds = new();
    private readonly ConcurrentBag<Func<BaseEntity, bool>> _armedMatches = [];

    public void Arm(string entityId) => _armedIds.TryAdd(entityId, 0);

    /// <summary>
    /// Arms the entities that the handler makes, so the test cannot know their IDs. Match on a parent ID that only
    /// the test knows.
    /// </summary>
    public void ArmWhere(Func<BaseEntity, bool> matches) => _armedMatches.Add(matches);

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
            .Any(e => e.State is not (EntityState.Unchanged or EntityState.Detached) && IsArmed(e.Entity));
        if (writesArmedEntity == true)
            throw new InvalidOperationException("The test failed this save.");
    }

    private bool IsArmed(BaseEntity entity)
        => _armedIds.ContainsKey(entity.Id) || _armedMatches.Any(matches => matches(entity));
}
