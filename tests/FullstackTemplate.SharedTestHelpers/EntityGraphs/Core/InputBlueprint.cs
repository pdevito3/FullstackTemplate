using System.Linq.Expressions;

namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>An immutable input factory with ordered traits and fill-only derivations.</summary>
/// <remarks>Inputs and outputs are reference types. Customizers and derives target the input, not the output. This is a root addition: Under does not implicitly populate its input or supply a parent.</remarks>
public sealed class InputBlueprint<TSeed, TInput, T>
    where TInput : class
    where T : class
{
    internal InputBlueprint(
        Func<TInput> newInput,
        Func<TInput, GraphContext<TSeed>, T> factory,
        Action<TInput, GraphContext<TSeed>>[] traits,
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

    internal Func<TInput, GraphContext<TSeed>, T> Factory { get; }
    internal Func<TInput> NewInput { get; }
    internal Action<TInput, GraphContext<TSeed>>[] Traits { get; }
    internal IDerivation<TSeed, TInput>[] Derives { get; }
    internal bool IsTransient { get; }

    /// <summary>Returns a variant with an additional trait, run before call-site customizers.</summary>
    public InputBlueprint<TSeed, TInput, T> With(Action<TInput> trait)
    {
        ArgumentNullException.ThrowIfNull(trait);
        return With((entity, _) => trait(entity));
    }

    /// <summary>Returns a variant with an additional context-aware trait.</summary>
    public InputBlueprint<TSeed, TInput, T> With(Action<TInput, GraphContext<TSeed>> trait)
    {
        ArgumentNullException.ThrowIfNull(trait);
        return new(NewInput, Factory, [.. Traits, trait], Derives, IsTransient);
    }

    /// <summary>Fills a directly writable property only when it still equals its type's default value.</summary>
    /// <remarks>This callback is fill-only, but calling ctx.Next inside it does not reserve explicit values for this property. Use Derive(property, sequence) for whole-step explicit-value reservation.</remarks>
    public InputBlueprint<TSeed, TInput, T> Derive<TProp>(
        Expression<Func<TInput, TProp>> property,
        Func<TInput, GraphContext<TSeed>, TProp> value
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            NewInput,
            Factory,
            Traits,
            [
                .. Derives,
                new Derivation<TSeed, TInput, TProp>(property, (entity, ctx) => value(entity, ctx)),
            ],
            IsTransient
        );
    }

    /// <summary>Fills an unset property from a sequence, reserving explicit values across the whole Add step first.</summary>
    /// <remarks>Use this overload for override preservation and whole-step explicit-value reservation. It reserves all explicit values before filling unset properties, unlike ctx.Next(sequence), which skips only values already reserved when called. Derivations run in declaration order; later steps cannot retroactively reserve earlier values.</remarks>
    public InputBlueprint<TSeed, TInput, T> Derive<TProp>(
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
    public InputBlueprint<TSeed, TInput, T> AsTransient() =>
        new(NewInput, Factory, Traits, Derives, true);
}
