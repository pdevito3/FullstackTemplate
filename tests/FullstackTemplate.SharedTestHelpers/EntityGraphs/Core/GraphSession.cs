namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>Owns mutable scenario state. Use one session per test; share only frozen options and blueprints.</summary>
/// <remarks>A factory, trait, customizer, derive, or completion failure faults the session. Further construction, navigation, transience, and saving then throw with the original failure as their inner exception. Start a new session; partially mutated user objects cannot be rolled back.</remarks>
public sealed class GraphSession<TSeed>
{
    private readonly IGraphStore store;
    private readonly TimeProvider? time;
    private readonly DateTimeOffset frozenNow;
    private readonly List<object> entities = [];
    private readonly Dictionary<object, object?> parents = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> transients = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, Dictionary<object, object>> sequences = new(
        ReferenceEqualityComparer.Instance
    );
    private readonly object[] seedEntities;
    private readonly HashSet<object> owned = new(ReferenceEqualityComparer.Instance);
    private Exception? failure;
    private object[] lastStep = [];
    private int buildStarted;

    internal GraphSession(
        TSeed seed,
        GraphOptions<TSeed> options,
        IGraphStore store,
        TimeProvider? time
    )
    {
        Seed = seed;
        Options = options;
        this.store = store;
        this.time = time;
        frozenNow = TimeProvider.System.GetUtcNow();
        seedEntities = options.GetSeedEntities(seed).ToArray();
        for (var i = 0; i < seedEntities.Length; i++)
        {
            if (seedEntities[i] is null)
            {
                throw new ArgumentException(
                    $"SeedEntities returned a null entity at index {i}.",
                    nameof(options)
                );
            }
            owned.Add(seedEntities[i]);
            parents.TryAdd(seedEntities[i], null);
        }
    }

    /// <summary>Gets the application-defined seed.</summary>
    public TSeed Seed { get; }
    internal GraphOptions<TSeed> Options { get; }
    internal DateTimeOffset Now
    {
        get
        {
            var now = (time?.GetUtcNow() ?? frozenNow).ToUniversalTime();
            var precision = Options.Precision.Ticks;
            return precision == 0 ? now : new(now.Ticks - now.Ticks % precision, TimeSpan.Zero);
        }
    }

    /// <summary>Gets created entities of the requested type, including transient entities and aggregate wrappers.</summary>
    public IReadOnlyList<T> All<T>() => entities.OfType<T>().ToArray();

    /// <summary>Gets the most recently recorded matching entity or throws.</summary>
    public T Last<T>()
    {
        for (var i = entities.Count - 1; i >= 0; i--)
        {
            if (entities[i] is T entity)
            {
                return entity;
            }
        }
        throw new InvalidOperationException(
            $"No entity of type {typeof(T).Name} has been created in this graph."
        );
    }

    private void EnsureHealthy()
    {
        if (failure is { } original)
        {
            throw new InvalidOperationException(
                $"This graph session is faulted because an earlier Add failed: {original.Message}",
                original
            );
        }
    }

    internal void EnsureOpen()
    {
        EnsureHealthy();
        if (Volatile.Read(ref buildStarted) != 0)
        {
            throw new InvalidOperationException(
                "This graph has already started BuildAsync and cannot be changed or built again."
            );
        }
    }

    // Preflight errors do not fault a session: no factory or mutable step state has run yet.
    internal void ValidateParents<TParent>(TParent[] values)
        where TParent : class
    {
        EnsureOpen();
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] is null)
            {
                throw new ArgumentException($"Parent at index {i} is null.", nameof(values));
            }
            if (!owned.Contains(values[i]))
            {
                throw new ArgumentException(
                    $"Parent at index {i} of type {values[i].GetType().Name} is not owned by this graph session. Use an Add output or a reference selected by SeedEntities; untracked aggregate parts cannot be parents.",
                    nameof(values)
                );
            }
        }
    }

    internal TResult ConstructStep<TResult>(Func<TResult> construct)
    {
        EnsureOpen();
        try
        {
            return construct();
        }
        catch (Exception exception)
        {
            failure ??= exception;
            lastStep = [];
            throw;
        }
    }

    private void ValidateAncestry<T>(List<(T Entity, GraphContext<TSeed> Context)> step)
        where T : class
    {
        var pending = new Dictionary<object, object?>(ReferenceEqualityComparer.Instance);
        foreach (var (entity, context) in step)
        {
            var parent = context.ParentEntity;
            if (
                parents.TryGetValue(entity, out var existing)
                || pending.TryGetValue(entity, out existing)
            )
            {
                if (!ReferenceEquals(existing, parent))
                {
                    var previous = existing?.GetType().Name ?? "root (no parent)";
                    var requested = parent?.GetType().Name ?? "root (no parent)";
                    throw new InvalidOperationException(
                        $"Entity {entity.GetType().Name} cannot change ancestry from {previous} to {requested}: the parent references conflict. Return a new instance for a different parent."
                    );
                }
            }
            pending.TryAdd(entity, parent);
        }
    }

    internal object FindAncestor(object? parent, Type type)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        while (parent is not null && seen.Add(parent))
        {
            if (type.IsInstanceOfType(parent))
            {
                return parent;
            }
            parent = parents.GetValueOrDefault(parent);
        }
        throw new InvalidOperationException(
            $"No ancestor of type {type.Name} exists for the current entity."
        );
    }

    private SequenceState<T> State<T>(Sequence<T> sequence, GraphContext<TSeed> context)
    {
        object scope = sequence.Scope switch
        {
            null => this,
            var type when type == typeof(ParentScope) => context.ParentEntity
                ?? throw new InvalidOperationException(
                    "A PerParent sequence requires a child entity with a parent."
                ),
            var type => FindAncestor(context.ParentEntity, type),
        };
        if (!sequences.TryGetValue(sequence, out var scopes))
        {
            sequences.Add(sequence, scopes = new(ReferenceEqualityComparer.Instance));
        }
        if (!scopes.TryGetValue(scope, out var state))
        {
            scopes.Add(scope, state = new SequenceState<T>(sequence.Start));
        }
        return (SequenceState<T>)state;
    }

    internal void Reserve<T>(Sequence<T> sequence, GraphContext<TSeed> context, T value) =>
        State(sequence, context).Used.Add(value);

    internal T Next<T>(Sequence<T> sequence, GraphContext<TSeed> context)
    {
        var state = State(sequence, context);
        var visited = new HashSet<T>();
        while (state.Used.Contains(state.Current))
        {
            if (!visited.Add(state.Current))
            {
                throw new InvalidOperationException(
                    "The sequence cannot advance to an unused value; its progression cycles through reserved values."
                );
            }
            state.Current = sequence.Advance(state.Current);
        }
        var result = state.Current;
        state.Used.Add(result);
        state.Current = sequence.Advance(result);
        return result;
    }

    internal T[] Complete<T>(
        List<(T Entity, GraphContext<TSeed> Context)> step,
        IReadOnlyList<IDerivation<TSeed, T>> derives,
        bool transient
    )
        where T : class
    {
        // Reject conflicting identity links before derives, defaults, or recording any member of the step.
        ValidateAncestry(step);
        var created = step.Select(item => item.Entity).ToArray();
        // Reserve the entire step before assigning a sequence, including explicit values later in the array.
        foreach (var derive in derives)
        {
            foreach (var (entity, context) in step)
            {
                derive.Reserve(entity, context);
            }
        }
        foreach (var (entity, context) in step)
        {
            foreach (var derive in derives)
            {
                derive.Apply(entity, context, created);
            }
            foreach (var applyDefault in Options.Defaults)
            {
                applyDefault(entity, context);
            }
            entities.Add(entity);
            owned.Add(entity);
            parents.TryAdd(entity, context.ParentEntity);
            if (transient)
            {
                transients.Add(entity);
            }
        }
        lastStep = created.Cast<object>().ToArray();
        return created;
    }

    internal void MarkTransient()
    {
        EnsureOpen();
        foreach (var entity in lastStep)
        {
            transients.Add(entity);
        }
    }

    internal async Task BuildAsync(CancellationToken cancellationToken)
    {
        EnsureHealthy();
        if (Interlocked.Exchange(ref buildStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "BuildAsync may only be called once per graph session."
            );
        }
        cancellationToken.ThrowIfCancellationRequested();
        // Expand exclusions first: transience wins even when a part was also included earlier.
        var excluded = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var transient in transients)
        {
            Walk(transient, entity => excluded.Add(entity));
        }
        List<object> collected = [];
        void Collect(object entity)
        {
            if (entity is not IGraphAggregate)
            {
                collected.Add(entity);
            }
        }
        foreach (var entity in seedEntities)
        {
            Walk(entity, Collect);
        }
        foreach (var entity in entities)
        {
            Walk(entity, Collect);
        }
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var rows = collected
            .Where(entity => !excluded.Contains(entity) && seen.Add(entity))
            .ToList();
        var context = new GraphSaveContext<TSeed>(this, rows);
        foreach (var hook in Options.BeforeHooks)
        {
            await hook(context, cancellationToken).ConfigureAwait(false);
        }
        await store.SaveAsync(rows, cancellationToken).ConfigureAwait(false);
        foreach (var hook in Options.AfterHooks)
        {
            await hook(context, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void Walk(object root, Action<object> visit)
    {
        var visiting = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Expand(object entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            visit(entity);
            if (entity is not IGraphAggregate aggregate)
            {
                return;
            }
            if (!visiting.Add(entity))
            {
                throw new InvalidOperationException(
                    "An aggregate contains a cycle in GetEntities()."
                );
            }
            foreach (var part in aggregate.GetEntities())
            {
                Expand(part);
            }
            visiting.Remove(entity);
        }
        Expand(root);
    }
}
