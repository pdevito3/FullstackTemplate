using Microsoft.EntityFrameworkCore;

namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>Persists a graph's rows through a <see cref="DbContext" /> with a single <c>SaveChangesAsync</c>.</summary>
/// <remarks>
/// <para>
/// Rows are added in creation order, then saved in one unit of work. EF Core sorts inserts by foreign-key
/// dependency, so set navigation properties (not key values) in child blueprints; EF fixes up foreign keys,
/// including database-generated ones.
/// </para>
/// <para>
/// EF Core also discovers entities reachable through navigations. A transient entity referenced by a persisted
/// row is therefore saved with it.
/// </para>
/// <para>
/// After the save, the store clears the change tracker, so the code under test loads fresh instances, the same
/// as a new request does.
/// </para>
/// <para>The store does not own the context: the caller creates, reuses and disposes it.</para>
/// </remarks>
/// <param name="db">The context that receives the rows.</param>
public sealed class DbContextStore(DbContext db) : IGraphStore
{
    private readonly DbContext db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc />
    public async ValueTask SaveAsync(
        IReadOnlyList<object> entities,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(entities);
        cancellationToken.ThrowIfCancellationRequested();
        db.AddRange(entities);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        db.ChangeTracker.Clear();
    }
}
