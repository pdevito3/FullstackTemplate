namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>Adds children only from a builder anchored to the matching parent array.</summary>
/// <remarks>Entities and parents must be reference types. Each parent must be owned by this session. The fixed-arity default Add overload avoids competing params overloads; context-aware AddOne requires its customizer. Factories, traits, and customizers are buffered before sequence reservation. Reusing a child identity is permitted only under the same parent reference; conflicting ancestry faults the session.</remarks>
public static class GraphChildExtensions
{
    extension<TSeed, TParent>(GraphBuilder<TSeed, TParent[]> builder)
        where TParent : class
    {
        /// <summary>Adds one default child per parent and re-anchors to its output.</summary>
        public GraphBuilder<TSeed, T[]> Add<T>(
            ChildBlueprint<TSeed, TParent, T> bp,
            out T[] created
        )
            where T : class => builder.AddChildCore(bp, out created, 1, null);

        /// <summary>Adds the requested count per parent and re-anchors to the output array.</summary>
        public GraphBuilder<TSeed, T[]> Add<T>(
            ChildBlueprint<TSeed, TParent, T> bp,
            out T[] created,
            int count
        )
            where T : class => builder.AddChildCore(bp, out created, count, null);

        /// <summary>Adds one output per customizer per parent; empty customizers create one default output.</summary>
        /// <remarks>Comma-separated delegates create separate entities. Combine delegates with + to stack traits on one entity.</remarks>
        public GraphBuilder<TSeed, T[]> Add<T>(
            ChildBlueprint<TSeed, TParent, T> bp,
            out T[] created,
            params Action<T>[] customize
        )
            where T : class
        {
            builder.Session.EnsureOpen();
            CustomizerValidation.Validate(customize);
            return builder.AddChildCore(
                bp,
                out created,
                Math.Max(1, customize.Length),
                customize.Length == 0 ? null : (entity, ctx) => customize[ctx.Index](entity)
            );
        }

        /// <summary>Adds one output per context-aware customizer per parent.</summary>
        /// <remarks>When mixing plain and context-aware traits, adapt plain traits to the context-aware form.</remarks>
        public GraphBuilder<TSeed, T[]> Add<T>(
            ChildBlueprint<TSeed, TParent, T> bp,
            out T[] created,
            params Action<T, ChildContext<TSeed, TParent>>[] customize
        )
            where T : class
        {
            builder.Session.EnsureOpen();
            CustomizerValidation.Validate(customize);
            return builder.AddChildCore(
                bp,
                out created,
                Math.Max(1, customize.Length),
                customize.Length == 0 ? null : (entity, ctx) => customize[ctx.Index](entity, ctx)
            );
        }

        /// <summary>Adds exactly one output with an optional customizer (requires exactly one parent).</summary>
        public GraphBuilder<TSeed, T[]> AddOne<T>(
            ChildBlueprint<TSeed, TParent, T> bp,
            out T created,
            Action<T>? customize = null
        )
            where T : class
        {
            builder.RequireSingleParent();
            var result = builder.AddChildCore(
                bp,
                out var items,
                1,
                customize is null ? null : (entity, _) => customize(entity)
            );
            created = items[0];
            return result;
        }

        /// <summary>Adds exactly one output with a context-aware customizer (requires exactly one parent).</summary>
        public GraphBuilder<TSeed, T[]> AddOne<T>(
            ChildBlueprint<TSeed, TParent, T> bp,
            out T created,
            Action<T, ChildContext<TSeed, TParent>> customize
        )
            where T : class
        {
            builder.Session.EnsureOpen();
            ArgumentNullException.ThrowIfNull(customize);
            builder.RequireSingleParent();
            var result = builder.AddChildCore(bp, out var items, 1, customize);
            created = items[0];
            return result;
        }

        private GraphBuilder<TSeed, T[]> AddChildCore<T>(
            ChildBlueprint<TSeed, TParent, T> bp,
            out T[] created,
            int count,
            Action<T, ChildContext<TSeed, TParent>>? customize
        )
            where T : class
        {
            builder.Session.EnsureOpen();
            ArgumentNullException.ThrowIfNull(bp);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            builder.Session.ValidateParents(builder.Anchor);
            created = builder.Session.ConstructStep(() =>
            {
                List<(T Entity, GraphContext<TSeed> Context)> step = [];
                for (var p = 0; p < builder.Anchor.Length; p++)
                {
                    var parent = builder.Anchor[p];
                    ArgumentNullException.ThrowIfNull(parent);
                    for (var i = 0; i < count; i++)
                    {
                        var ctx = new ChildContext<TSeed, TParent>(builder.Session, i, parent, p);
                        var entity =
                            bp.Factory(parent, ctx)
                            ?? throw new InvalidOperationException(
                                "A blueprint factory returned null."
                            );
                        foreach (var trait in bp.Traits)
                        {
                            trait(entity, ctx);
                        }
                        customize?.Invoke(entity, ctx);
                        step.Add((entity, ctx));
                    }
                }
                return builder.Session.Complete(step, bp.Derives, bp.IsTransient);
            });
            return new(builder.Session, created);
        }

        /// <summary>Adds one default child per parent and re-anchors to its output.</summary>
        public GraphBuilder<TSeed, T[]> Add<TInput, T>(
            ChildInputBlueprint<TSeed, TParent, TInput, T> bp,
            out T[] created
        )
            where TInput : class
            where T : class => builder.AddChildInputCore(bp, out created, 1, null);

        /// <summary>Adds the requested count per parent and re-anchors to the output array.</summary>
        public GraphBuilder<TSeed, T[]> Add<TInput, T>(
            ChildInputBlueprint<TSeed, TParent, TInput, T> bp,
            out T[] created,
            int count
        )
            where TInput : class
            where T : class => builder.AddChildInputCore(bp, out created, count, null);

        /// <summary>Adds one output per customizer per parent; empty customizers create one default output.</summary>
        /// <remarks>Comma-separated delegates create separate entities. Combine delegates with + to stack traits on one entity.</remarks>
        public GraphBuilder<TSeed, T[]> Add<TInput, T>(
            ChildInputBlueprint<TSeed, TParent, TInput, T> bp,
            out T[] created,
            params Action<TInput>[] customize
        )
            where TInput : class
            where T : class
        {
            builder.Session.EnsureOpen();
            CustomizerValidation.Validate(customize);
            return builder.AddChildInputCore(
                bp,
                out created,
                Math.Max(1, customize.Length),
                customize.Length == 0 ? null : (entity, ctx) => customize[ctx.Index](entity)
            );
        }

        /// <summary>Adds one output per context-aware customizer per parent.</summary>
        /// <remarks>When mixing plain and context-aware traits, adapt plain traits to the context-aware form.</remarks>
        public GraphBuilder<TSeed, T[]> Add<TInput, T>(
            ChildInputBlueprint<TSeed, TParent, TInput, T> bp,
            out T[] created,
            params Action<TInput, ChildContext<TSeed, TParent>>[] customize
        )
            where TInput : class
            where T : class
        {
            builder.Session.EnsureOpen();
            CustomizerValidation.Validate(customize);
            return builder.AddChildInputCore(
                bp,
                out created,
                Math.Max(1, customize.Length),
                customize.Length == 0 ? null : (entity, ctx) => customize[ctx.Index](entity, ctx)
            );
        }

        /// <summary>Adds exactly one output with an optional customizer (requires exactly one parent).</summary>
        public GraphBuilder<TSeed, T[]> AddOne<TInput, T>(
            ChildInputBlueprint<TSeed, TParent, TInput, T> bp,
            out T created,
            Action<TInput>? customize = null
        )
            where TInput : class
            where T : class
        {
            builder.RequireSingleParent();
            var result = builder.AddChildInputCore(
                bp,
                out var items,
                1,
                customize is null ? null : (entity, _) => customize(entity)
            );
            created = items[0];
            return result;
        }

        /// <summary>Adds exactly one output with a context-aware customizer (requires exactly one parent).</summary>
        public GraphBuilder<TSeed, T[]> AddOne<TInput, T>(
            ChildInputBlueprint<TSeed, TParent, TInput, T> bp,
            out T created,
            Action<TInput, ChildContext<TSeed, TParent>> customize
        )
            where TInput : class
            where T : class
        {
            builder.Session.EnsureOpen();
            ArgumentNullException.ThrowIfNull(customize);
            builder.RequireSingleParent();
            var result = builder.AddChildInputCore(bp, out var items, 1, customize);
            created = items[0];
            return result;
        }

        private GraphBuilder<TSeed, T[]> AddChildInputCore<TInput, T>(
            ChildInputBlueprint<TSeed, TParent, TInput, T> bp,
            out T[] created,
            int count,
            Action<TInput, ChildContext<TSeed, TParent>>? customize
        )
            where TInput : class
            where T : class
        {
            builder.Session.EnsureOpen();
            ArgumentNullException.ThrowIfNull(bp);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            builder.Session.ValidateParents(builder.Anchor);
            created = builder.Session.ConstructStep(() =>
            {
                List<(TInput Entity, GraphContext<TSeed> Context)> step = [];
                for (var p = 0; p < builder.Anchor.Length; p++)
                {
                    var parent = builder.Anchor[p];
                    ArgumentNullException.ThrowIfNull(parent);
                    for (var i = 0; i < count; i++)
                    {
                        var ctx = new ChildContext<TSeed, TParent>(builder.Session, i, parent, p);
                        var entity =
                            bp.NewInput()
                            ?? throw new InvalidOperationException(
                                "A blueprint factory returned null."
                            );
                        foreach (var trait in bp.Traits)
                        {
                            trait(entity, ctx);
                        }
                        customize?.Invoke(entity, ctx);
                        step.Add((entity, ctx));
                    }
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
                foreach (var (input, context) in step)
                {
                    var ctx = (ChildContext<TSeed, TParent>)context;
                    foreach (var derive in bp.Derives)
                    {
                        derive.Apply(input, ctx, inputs);
                    }
                    var entity =
                        bp.Factory(ctx.Parent, input, ctx)
                        ?? throw new InvalidOperationException(
                            "An input blueprint factory returned null."
                        );
                    outputs.Add((entity, ctx));
                }
                return builder.Session.Complete(outputs, [], bp.IsTransient);
            });
            return new(builder.Session, created);
        }

        private void RequireSingleParent()
        {
            builder.Session.EnsureOpen();
            if (builder.Anchor.Length != 1)
            {
                throw new InvalidOperationException(
                    "A singular child AddOne requires exactly one parent; use Add for fan-out."
                );
            }
        }
    }
}
