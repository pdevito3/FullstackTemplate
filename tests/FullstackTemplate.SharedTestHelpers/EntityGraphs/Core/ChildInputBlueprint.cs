using System.Linq.Expressions;

namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>An immutable child input factory with ordered traits and fill-only derivations.</summary>
/// <remarks>Inputs and outputs are reference types. Customizers and derives target the input, not the output. The factory receives the typed parent; inputs remain unrecorded construction data.</remarks>
public sealed class ChildInputBlueprint<TSeed, TParent, TInput, T>
    where TParent : class
    where TInput : class
    where T : class
{
    internal ChildInputBlueprint(
        Func<TInput> newInput,
        Func<TParent, TInput, ChildContext<TSeed, TParent>, T> factory,
        Action<TInput, ChildContext<TSeed, TParent>>[] traits,
        IDerivation<TSeed, TInput>[] derives,
        bool transient
    )
    {
        Factory = factory;
        Traits = traits;
        Derives = derives;
        IsTransient = transient;
        NewInput = newInput;
    }

    internal Func<TParent, TInput, ChildContext<TSeed, TParent>, T> Factory { get; }
    internal Func<TInput> NewInput { get; }
    internal Action<TInput, ChildContext<TSeed, TParent>>[] Traits { get; }
    internal IDerivation<TSeed, TInput>[] Derives { get; }
    internal bool IsTransient { get; }

    /// <summary>Returns a variant with an additional trait, run before call-site customizers.</summary>
    public ChildInputBlueprint<TSeed, TParent, TInput, T> With(Action<TInput> trait)
    {
        ArgumentNullException.ThrowIfNull(trait);
        return With((entity, _) => trait(entity));
    }

    /// <summary>Returns a variant with an additional context-aware trait.</summary>
    public ChildInputBlueprint<TSeed, TParent, TInput, T> With(
        Action<TInput, ChildContext<TSeed, TParent>> trait
    )
    {
        ArgumentNullException.ThrowIfNull(trait);
        return new(NewInput, Factory, [.. Traits, trait], Derives, IsTransient);
    }

    /// <summary>Fills a directly writable property only when it still equals its type's default value.</summary>
    /// <remarks>This callback is fill-only, but calling ctx.Next inside it does not reserve explicit values for this property. Use Derive(property, sequence) for whole-step explicit-value reservation. To see the other members of the Add step, use the overload whose function also gets the step.</remarks>
    public ChildInputBlueprint<TSeed, TParent, TInput, T> Derive<TProp>(
        Expression<Func<TInput, TProp>> property,
        Func<TInput, ChildContext<TSeed, TParent>, TProp> value
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            NewInput,
            Factory,
            Traits,
            [
                .. Derives,
                new Derivation<TSeed, TInput, TProp>(
                    property,
                    (entity, ctx, _) => value(entity, (ChildContext<TSeed, TParent>)ctx)
                ),
            ],
            IsTransient
        );
    }

    /// <summary>Fills a directly writable property only when it still equals its type's default value. The function also gets every member of the Add step.</summary>
    /// <remarks>The step holds the members of the current Add step in order, this member included. All customizers run before the first derive, so the step holds the explicit values of every member. Earlier members also hold the values that their derives filled. Use this overload when a derived value must not take a value of another member, for example the lowest free number. In a fan-out under more than one parent, the step holds the members of every parent.</remarks>
    public ChildInputBlueprint<TSeed, TParent, TInput, T> Derive<TProp>(
        Expression<Func<TInput, TProp>> property,
        Func<TInput, ChildContext<TSeed, TParent>, IReadOnlyList<TInput>, TProp> value
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            NewInput,
            Factory,
            Traits,
            [
                .. Derives,
                new Derivation<TSeed, TInput, TProp>(
                    property,
                    (entity, ctx, step) => value(entity, (ChildContext<TSeed, TParent>)ctx, step)
                ),
            ],
            IsTransient
        );
    }

    /// <summary>Fills an unset property from a sequence, reserving explicit values across the whole Add step first.</summary>
    /// <remarks>Use this overload for override preservation and whole-step explicit-value reservation. It reserves all explicit values before filling unset properties, unlike ctx.Next(sequence), which skips only values already reserved when called. Derivations run in declaration order; later steps cannot retroactively reserve earlier values.</remarks>
    public ChildInputBlueprint<TSeed, TParent, TInput, T> Derive<TProp>(
        Expression<Func<TInput, TProp>> property,
        Sequence<TProp> sequence
    )
    {
        ArgumentNullException.ThrowIfNull(sequence);
        return new(
            NewInput,
            Factory,
            Traits,
            [.. Derives, new Derivation<TSeed, TInput, TProp>(property, null, sequence)],
            IsTransient
        );
    }

    /// <summary>Returns a variant whose output (including aggregate parts) is excluded from persistence.</summary>
    /// <remarks>Exclusion wins by reference identity, even if a part is also selected by SeedEntities, returned by another aggregate, or added separately.</remarks>
    public ChildInputBlueprint<TSeed, TParent, TInput, T> AsTransient() =>
        new(NewInput, Factory, Traits, Derives, true);
}
