using System.Linq.Expressions;

namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>Configures defaults and persistence; frozen on first use and safe to share across sessions.</summary>
public sealed class GraphOptions<TSeed>
{
    private readonly object gate = new();
    private bool frozen;
    internal Func<TSeed, IEnumerable<object>> GetSeedEntities { get; private set; } = _ => [];
    internal List<Action<object, GraphContext<TSeed>>> Defaults { get; } = [];
    internal List<
        Func<GraphSaveContext<TSeed>, CancellationToken, ValueTask>
    > BeforeHooks { get; } = [];
    internal List<Func<GraphSaveContext<TSeed>, CancellationToken, ValueTask>> AfterHooks { get; } =
    [];
    internal TimeSpan Precision { get; private set; } = TimeSpan.FromTicks(10);
    // The core has no Server references, so it has no default ID format. Call Ids to set one.
    internal Func<string, string>? NewId { get; private set; }

    /// <summary>Selects baseline rows to persist before created entities.</summary>
    /// <remarks>Evaluated and snapshotted once at EntityGraph.Start. Directly selected references qualify as session-owned parents for Under. Aggregate parts are expanded only for persistence and are not automatically tracked as parents. Null selected entries are rejected.</remarks>
    public GraphOptions<TSeed> SeedEntities(Func<TSeed, IEnumerable<object>> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return Change(() => GetSeedEntities = entities);
    }

    /// <summary>Fills an unset property on entities matching a class or interface.</summary>
    public GraphOptions<TSeed> Default<TBase, TProp>(
        Expression<Func<TBase, TProp>> property,
        Func<TBase, GraphContext<TSeed>, TProp> value
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        var derive = new Derivation<TSeed, TBase, TProp>(property, (entity, ctx, _) => value(entity, ctx));
        return Change(() =>
            Defaults.Add(
                (entity, ctx) =>
                {
                    if (entity is TBase typed)
                    {
                        derive.Apply(typed, ctx, []);
                    }
                }
            )
        );
    }

    /// <summary>Registers a hook that may add rows before the store saves.</summary>
    public GraphOptions<TSeed> BeforeSave(
        Func<GraphSaveContext<TSeed>, CancellationToken, ValueTask> hook
    )
    {
        ArgumentNullException.ThrowIfNull(hook);
        return Change(() => BeforeHooks.Add(hook));
    }

    /// <summary>Registers a hook invoked after the store successfully saves.</summary>
    public GraphOptions<TSeed> AfterSave(
        Func<GraphSaveContext<TSeed>, CancellationToken, ValueTask> hook
    )
    {
        ArgumentNullException.ThrowIfNull(hook);
        return Change(() => AfterHooks.Add(hook));
    }

    /// <summary>Sets UTC time truncation precision; zero disables truncation. Default is one microsecond.</summary>
    public GraphOptions<TSeed> TimePrecision(TimeSpan precision)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(precision, TimeSpan.Zero);
        return Change(() => Precision = precision);
    }

    /// <summary>Sets the ID generator that <c>ctx.NewId</c> uses. A session has no default generator.</summary>
    public GraphOptions<TSeed> Ids(Func<string, string> newId)
    {
        ArgumentNullException.ThrowIfNull(newId);
        return Change(() => NewId = newId);
    }

    internal void Freeze()
    {
        lock (gate)
        {
            frozen = true;
        }
    }

    private GraphOptions<TSeed> Change(Action change)
    {
        lock (gate)
        {
            if (frozen)
            {
                throw new InvalidOperationException(
                    "EntityGraph options are frozen after EntityGraph.Start; configure them before first use."
                );
            }
            change();
        }
        return this;
    }
}

/// <summary>Provides the mutable persistence rows and scenario inspection to save hooks.</summary>
public sealed class GraphSaveContext<TSeed>
{
    internal GraphSaveContext(GraphSession<TSeed> session, List<object> entities)
    {
        Session = session;
        Entities = entities;
    }

    /// <summary>Gets the application's seed.</summary>
    public TSeed Seed => Session.Seed;

    /// <summary>Gets rows to save; before-save hooks may add or remove rows.</summary>
    public List<object> Entities { get; }

    /// <summary>Gets the session for inspecting created entities.</summary>
    public GraphSession<TSeed> Session { get; }
}
