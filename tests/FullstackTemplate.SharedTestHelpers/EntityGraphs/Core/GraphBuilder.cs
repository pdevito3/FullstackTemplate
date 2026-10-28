namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>A fluent view of a session and its current typed anchor. Adds build eagerly; BuildAsync saves once.</summary>
/// <remarks>Entities, child parents, and inputs are reference types; seeds may have any shape. Factories, traits, and customizers are buffered across a step before sequence reservation. The fixed-arity default Add overload avoids ambiguity between the two params delegate forms. Context-aware AddOne requires its customizer; the plain optional-customizer overload supplies the default case. Construction failures fault the session; preflight argument errors do not.</remarks>
public sealed class GraphBuilder<TSeed, TAnchor>
{
    internal GraphBuilder(GraphSession<TSeed> session, TAnchor anchor)
    {
        Session = session;
        Anchor = anchor;
    }

    /// <summary>Gets the shared session behind all branches of this builder.</summary>
    public GraphSession<TSeed> Session { get; }

    /// <summary>Gets the seed or output array anchoring the current branch.</summary>
    public TAnchor Anchor { get; }

    /// <summary>Re-anchors to a session-owned parent array without adding entities.</summary>
    /// <remarks>Every reference must be an Add output or selected directly by SeedEntities at Start. Aggregate parts are not automatically tracked and cannot be parents unless separately added or selected. Null and foreign entries report their array index. The array is copied so later caller mutation cannot change this anchor.</remarks>
    public GraphBuilder<TSeed, TParent[]> Under<TParent>(TParent[] parents)
        where TParent : class
    {
        Session.EnsureOpen();
        ArgumentNullException.ThrowIfNull(parents);
        Session.ValidateParents(parents);
        return new(Session, (TParent[])parents.Clone());
    }

    /// <summary>Re-anchors to one session-owned parent without adding it.</summary>
    /// <remarks>Only Add outputs and references directly selected by SeedEntities qualify; untracked aggregate parts are rejected.</remarks>
    public GraphBuilder<TSeed, TParent[]> Under<TParent>(TParent parent)
        where TParent : class
    {
        Session.EnsureOpen();
        ArgumentNullException.ThrowIfNull(parent);
        return Under([parent]);
    }

    /// <summary>Excludes outputs of the session's most recent Add step from persistence.</summary>
    public GraphBuilder<TSeed, TAnchor> Transient()
    {
        Session.MarkTransient();
        return this;
    }

    /// <summary>Saves the session once, running before-save and after-save hooks.</summary>
    /// <remarks>A faulted Add prevents saving any partially constructed rows and reports its original exception. Transient aggregate exclusions win over overlapping seed rows, other aggregates, and separate Add outputs.</remarks>
    public Task BuildAsync(CancellationToken ct = default) => Session.BuildAsync(ct);

    /// <summary>Gets recorded entities of a type in creation order, including transient outputs.</summary>
    public IReadOnlyList<T> All<T>() => Session.All<T>();

    /// <summary>Gets the last recorded entity of a type or throws.</summary>
    public T Last<T>() => Session.Last<T>();

    /// <summary>Adds one default entity and re-anchors to its output.</summary>
    public GraphBuilder<TSeed, T[]> Add<T>(Blueprint<TSeed, T> bp, out T[] created)
        where T : class => AddCore(bp, out created, 1, null);

    /// <summary>Adds the requested count and re-anchors to the output array.</summary>
    public GraphBuilder<TSeed, T[]> Add<T>(Blueprint<TSeed, T> bp, out T[] created, int count)
        where T : class => AddCore(bp, out created, count, null);

    /// <summary>Adds one output per customizer; empty customizers create one default output.</summary>
    /// <remarks>Comma-separated delegates create separate entities. Combine delegates with + to stack traits on one entity.</remarks>
    public GraphBuilder<TSeed, T[]> Add<T>(
        Blueprint<TSeed, T> bp,
        out T[] created,
        params Action<T>[] customize
    )
        where T : class
    {
        Session.EnsureOpen();
        CustomizerValidation.Validate(customize);
        return AddCore(
            bp,
            out created,
            Math.Max(1, customize.Length),
            customize.Length == 0 ? null : (entity, ctx) => customize[ctx.Index](entity)
        );
    }

    /// <summary>Adds one output per context-aware customizer.</summary>
    /// <remarks>When mixing plain and context-aware traits, adapt plain traits to the context-aware form.</remarks>
    public GraphBuilder<TSeed, T[]> Add<T>(
        Blueprint<TSeed, T> bp,
        out T[] created,
        params Action<T, GraphContext<TSeed>>[] customize
    )
        where T : class
    {
        Session.EnsureOpen();
        CustomizerValidation.Validate(customize);
        return AddCore(
            bp,
            out created,
            Math.Max(1, customize.Length),
            customize.Length == 0 ? null : (entity, ctx) => customize[ctx.Index](entity, ctx)
        );
    }

    /// <summary>Adds exactly one output with an optional customizer.</summary>
    public GraphBuilder<TSeed, T[]> AddOne<T>(
        Blueprint<TSeed, T> bp,
        out T created,
        Action<T>? customize = null
    )
        where T : class
    {
        var result = AddCore(
            bp,
            out var items,
            1,
            customize is null ? null : (entity, _) => customize(entity)
        );
        created = items[0];
        return result;
    }

    /// <summary>Adds exactly one output with a context-aware customizer.</summary>
    public GraphBuilder<TSeed, T[]> AddOne<T>(
        Blueprint<TSeed, T> bp,
        out T created,
        Action<T, GraphContext<TSeed>> customize
    )
        where T : class
    {
        Session.EnsureOpen();
        ArgumentNullException.ThrowIfNull(customize);

        var result = AddCore(bp, out var items, 1, customize);
        created = items[0];
        return result;
    }

    private GraphBuilder<TSeed, T[]> AddCore<T>(
        Blueprint<TSeed, T> bp,
        out T[] created,
        int count,
        Action<T, GraphContext<TSeed>>? customize
    )
        where T : class
    {
        Session.EnsureOpen();
        ArgumentNullException.ThrowIfNull(bp);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        created = Session.ConstructStep(() =>
        {
            List<(T Entity, GraphContext<TSeed> Context)> step = [];
            for (var i = 0; i < count; i++)
            {
                var ctx = new GraphContext<TSeed>(Session, i, null);
                var entity =
                    bp.Factory(ctx)
                    ?? throw new InvalidOperationException("A blueprint factory returned null.");
                foreach (var trait in bp.Traits)
                {
                    trait(entity, ctx);
                }
                customize?.Invoke(entity, ctx);
                step.Add((entity, ctx));
            }
            return Session.Complete(step, bp.Derives, bp.IsTransient);
        });
        return new(Session, created);
    }

    /// <summary>Adds one default output and re-anchors to its output.</summary>
    public GraphBuilder<TSeed, T[]> Add<TInput, T>(
        InputBlueprint<TSeed, TInput, T> bp,
        out T[] created
    )
        where TInput : class
        where T : class => AddInputCore(bp, out created, 1, null);

    /// <summary>Adds the requested count and re-anchors to the output array.</summary>
    public GraphBuilder<TSeed, T[]> Add<TInput, T>(
        InputBlueprint<TSeed, TInput, T> bp,
        out T[] created,
        int count
    )
        where TInput : class
        where T : class => AddInputCore(bp, out created, count, null);

    /// <summary>Adds one output per customizer; empty customizers create one default output.</summary>
    /// <remarks>Comma-separated delegates create separate entities. Combine delegates with + to stack traits on one entity.</remarks>
    public GraphBuilder<TSeed, T[]> Add<TInput, T>(
        InputBlueprint<TSeed, TInput, T> bp,
        out T[] created,
        params Action<TInput>[] customize
    )
        where TInput : class
        where T : class
    {
        Session.EnsureOpen();
        CustomizerValidation.Validate(customize);
        return AddInputCore(
            bp,
            out created,
            Math.Max(1, customize.Length),
            customize.Length == 0 ? null : (entity, ctx) => customize[ctx.Index](entity)
        );
    }

    /// <summary>Adds one output per context-aware customizer.</summary>
    /// <remarks>When mixing plain and context-aware traits, adapt plain traits to the context-aware form.</remarks>
    public GraphBuilder<TSeed, T[]> Add<TInput, T>(
        InputBlueprint<TSeed, TInput, T> bp,
        out T[] created,
        params Action<TInput, GraphContext<TSeed>>[] customize
    )
        where TInput : class
        where T : class
    {
        Session.EnsureOpen();
        CustomizerValidation.Validate(customize);
        return AddInputCore(
            bp,
            out created,
            Math.Max(1, customize.Length),
            customize.Length == 0 ? null : (entity, ctx) => customize[ctx.Index](entity, ctx)
        );
    }

    /// <summary>Adds exactly one output with an optional customizer.</summary>
    public GraphBuilder<TSeed, T[]> AddOne<TInput, T>(
        InputBlueprint<TSeed, TInput, T> bp,
        out T created,
        Action<TInput>? customize = null
    )
        where TInput : class
        where T : class
    {
        var result = AddInputCore(
            bp,
            out var items,
            1,
            customize is null ? null : (entity, _) => customize(entity)
        );
        created = items[0];
        return result;
    }

    /// <summary>Adds exactly one output with a context-aware customizer.</summary>
    public GraphBuilder<TSeed, T[]> AddOne<TInput, T>(
        InputBlueprint<TSeed, TInput, T> bp,
        out T created,
        Action<TInput, GraphContext<TSeed>> customize
    )
        where TInput : class
        where T : class
    {
        Session.EnsureOpen();
        ArgumentNullException.ThrowIfNull(customize);

        var result = AddInputCore(bp, out var items, 1, customize);
        created = items[0];
        return result;
    }

    private GraphBuilder<TSeed, T[]> AddInputCore<TInput, T>(
        InputBlueprint<TSeed, TInput, T> bp,
        out T[] created,
        int count,
        Action<TInput, GraphContext<TSeed>>? customize
    )
        where TInput : class
        where T : class
    {
        Session.EnsureOpen();
        ArgumentNullException.ThrowIfNull(bp);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        created = Session.ConstructStep(() =>
        {
            List<(TInput Entity, GraphContext<TSeed> Context)> step = [];
            for (var i = 0; i < count; i++)
            {
                var ctx = new GraphContext<TSeed>(Session, i, null);
                var entity =
                    bp.NewInput()
                    ?? throw new InvalidOperationException("A blueprint factory returned null.");
                foreach (var trait in bp.Traits)
                {
                    trait(entity, ctx);
                }
                customize?.Invoke(entity, ctx);
                step.Add((entity, ctx));
            }
            foreach (var derive in bp.Derives)
            {
                foreach (var (input, ctx) in step)
                {
                    derive.Reserve(input, ctx);
                }
            }
            var inputs = step.Select(item => item.Entity).ToArray();
            List<(T Entity, GraphContext<TSeed> Context)> outputs = [];
            foreach (var (input, ctx) in step)
            {
                foreach (var derive in bp.Derives)
                {
                    derive.Apply(input, ctx, inputs);
                }
                var entity =
                    bp.Factory(input, ctx)
                    ?? throw new InvalidOperationException(
                        "An input blueprint factory returned null."
                    );
                outputs.Add((entity, ctx));
            }
            return Session.Complete(outputs, [], bp.IsTransient);
        });
        return new(Session, created);
    }
}
