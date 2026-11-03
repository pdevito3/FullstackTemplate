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
    private readonly ConcurrentBag<Func<BaseEntity, bool>> _armedAfterCommit = [];
    private readonly ConcurrentDictionary<DbContext, byte> _committing = new();

    public void Arm(string entityId) => _armedIds.TryAdd(entityId, 0);

    /// <summary>
    /// Arms the entities that the handler makes, so the test cannot know their IDs. Match on a parent ID that only
    /// the test knows.
    /// </summary>
    public void ArmWhere(Func<BaseEntity, bool> matches) => _armedMatches.Add(matches);

    /// <summary>
    /// Fails each save that writes a matching entity after the save commits, as a lost reply of the commit does. The
    /// row is in the database, and the handler gets an error.
    /// </summary>
    public void ArmAfterCommitWhere(Func<BaseEntity, bool> matches) => _armedAfterCommit.Add(matches);

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

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && _committing.TryRemove(context, out _))
            throw new InvalidOperationException("The test failed this save after its commit.");
        return ValueTask.FromResult(result);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
            _committing.TryRemove(context, out _);
        return Task.CompletedTask;
    }

    private void FailWhenArmed(DbContext? context)
    {
        if (context is null)
            return;
        var written = context.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State is not (EntityState.Unchanged or EntityState.Detached))
            .Select(e => e.Entity)
            .ToList();
        if (written.Any(IsArmed))
            throw new InvalidOperationException("The test failed this save.");
        if (written.Any(entity => _armedAfterCommit.Any(matches => matches(entity))))
            _committing.TryAdd(context, 0);
    }

    private bool IsArmed(BaseEntity entity)
        => _armedIds.ContainsKey(entity.Id) || _armedMatches.Any(matches => matches(entity));
}
