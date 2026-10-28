namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>Supplies seed, time, sequences, and graph inspection while building an entity.</summary>
public class GraphContext<TSeed>
{
    internal GraphContext(GraphSession<TSeed> session, int index, object? parent)
    {
        Session = session;
        Index = index;
        ParentEntity = parent;
    }

    internal GraphSession<TSeed> Session { get; }
    internal object? ParentEntity { get; }

    /// <summary>Gets the application's baseline seed.</summary>
    public TSeed Seed => Session.Seed;

    /// <summary>Gets the position in this Add call (per parent for a child fan-out).</summary>
    public int Index { get; }

    /// <summary>Gets precision-rounded UTC time; frozen unless a provider was supplied.</summary>
    public DateTimeOffset Now => Session.Now;

    /// <summary>Returns a time before the rounded current time.</summary>
    public DateTimeOffset Ago(TimeSpan duration) => Now - duration;

    /// <summary>Returns a time after the rounded current time.</summary>
    public DateTimeOffset FromNow(TimeSpan duration) => Now + duration;

    /// <summary>Creates an ID with the generator from <c>GraphOptions.Ids</c>. Throws if the session has no generator.</summary>
    public string NewId(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var newId = Session.Options.NewId
            ?? throw new InvalidOperationException("The session has no ID generator. Call GraphOptions.Ids to set one.");
        return newId(prefix);
    }

    /// <summary>Advances a sequence immediately, skipping only values already reserved when called.</summary>
    /// <remarks>Next does not preserve a later customizer override or reserve explicit values across the current step. For override preservation and whole-step explicit-value reservation, use Blueprint.Derive(property, sequence) (or its child/input equivalent). A factory calling Next runs before those reservations and may consume a value that a later customizer explicitly sets.</remarks>
    public TValue Next<TValue>(Sequence<TValue> sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        return Session.Next(sequence, this);
    }

    /// <summary>Finds the nearest recorded ancestor of the requested type or throws.</summary>
    public TAncestor Ancestor<TAncestor>() =>
        (TAncestor)Session.FindAncestor(ParentEntity, typeof(TAncestor));

    /// <summary>Gets matching entities recorded so far in creation order.</summary>
    /// <remarks>Factories, traits, and customizers see completed earlier steps while the current step is buffered for sequence reservation. Entity derives and defaults also see earlier outputs already recorded in the current step; input derives and input factories see earlier completed steps. A derive that must see the other members of its own step uses the Derive overload whose function also gets the step.</remarks>
    public IReadOnlyList<T> All<T>() => Session.All<T>();

    /// <summary>Gets the last recorded matching entity or throws.</summary>
    /// <remarks>Like All, factories, traits, and customizers see only earlier completed steps. Entity derives/defaults can see earlier recorded outputs in their current step; input derives and input factories see earlier completed steps. The entity currently being built is not yet recorded. Last throws if no matching output is visible.</remarks>
    public T Last<T>() => Session.Last<T>();
}

/// <summary>Supplies a typed parent and its fan-out index when building a child.</summary>
public sealed class ChildContext<TSeed, TParent> : GraphContext<TSeed>
{
    internal ChildContext(GraphSession<TSeed> session, int index, TParent parent, int parentIndex)
        : base(session, index, parent)
    {
        Parent = parent;
        ParentIndex = parentIndex;
    }

    /// <summary>Gets this child's parent.</summary>
    public TParent Parent { get; }

    /// <summary>Gets the parent's position in the anchor array.</summary>
    public int ParentIndex { get; }
}
