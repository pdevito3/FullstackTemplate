namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>Persists a completed scenario in creation order.</summary>
public interface IGraphStore
{
    /// <summary>Saves the supplied rows once.</summary>
    ValueTask SaveAsync(IReadOnlyList<object> entities, CancellationToken cancellationToken);
}

/// <summary>An aggregate whose parts are persisted in place of the aggregate.</summary>
public interface IGraphAggregate
{
    /// <summary>Returns rows or nested aggregates in persistence order.</summary>
    IEnumerable<object> GetEntities();
}

/// <summary>A store for scenarios that do not require persistence.</summary>
public sealed class NullStore : IGraphStore
{
    private NullStore() { }

    /// <summary>Gets the shared stateless store.</summary>
    public static NullStore Instance { get; } = new();

    /// <inheritdoc />
    public ValueTask SaveAsync(IReadOnlyList<object> entities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
