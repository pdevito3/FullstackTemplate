using System.Linq.Expressions;

namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>An immutable entity factory with ordered traits and fill-only derivations.</summary>
/// <remarks>Entities are reference types so trait mutation and identity links remain stable.</remarks>
public sealed class Blueprint<TSeed, T>
    where T : class
{
    internal Blueprint(
        Func<GraphContext<TSeed>, T> factory,
        Action<T, GraphContext<TSeed>>[] traits,
        IDerivation<TSeed, T>[] derives,
        bool transient
    )
    {
        Factory = factory;
        Traits = traits;
        Derives = derives;
        IsTransient = transient;
    }

    internal Func<GraphContext<TSeed>, T> Factory { get; }

    internal Action<T, GraphContext<TSeed>>[] Traits { get; }
    internal IDerivation<TSeed, T>[] Derives { get; }
    internal bool IsTransient { get; }

    /// <summary>Returns a variant with an additional trait, run before call-site customizers.</summary>
    public Blueprint<TSeed, T> With(Action<T> trait)
    {
        ArgumentNullException.ThrowIfNull(trait);
        return With((entity, _) => trait(entity));
    }

    /// <summary>Returns a variant with an additional context-aware trait.</summary>
    public Blueprint<TSeed, T> With(Action<T, GraphContext<TSeed>> trait)
    {
        ArgumentNullException.ThrowIfNull(trait);
        return new(Factory, [.. Traits, trait], Derives, IsTransient);
    }

    /// <summary>Fills a directly writable property only when it still equals its type's default value.</summary>
    /// <remarks>This callback is fill-only, but calling ctx.Next inside it does not reserve explicit values for this property. Use Derive(property, sequence) for whole-step explicit-value reservation.</remarks>
    public Blueprint<TSeed, T> Derive<TProp>(
        Expression<Func<T, TProp>> property,
        Func<T, GraphContext<TSeed>, TProp> value
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            Factory,
            Traits,
            [
                .. Derives,
                new Derivation<TSeed, T, TProp>(property, (entity, ctx) => value(entity, ctx)),
            ],
            IsTransient
        );
    }

    /// <summary>Fills an unset property from a sequence, reserving explicit values across the whole Add step first.</summary>
    /// <remarks>Use this overload for override preservation and whole-step explicit-value reservation. It reserves all explicit values before filling unset properties, unlike ctx.Next(sequence), which skips only values already reserved when called. Derivations run in declaration order; later steps cannot retroactively reserve earlier values.</remarks>
    public Blueprint<TSeed, T> Derive<TProp>(
        Expression<Func<T, TProp>> property,
        Sequence<TProp> sequence
    )
    {
        ArgumentNullException.ThrowIfNull(sequence);
        return new(
            Factory,
            Traits,
            [.. Derives, new Derivation<TSeed, T, TProp>(property, null, sequence)],
            IsTransient
        );
    }

    /// <summary>Returns a variant whose output (including aggregate parts) is excluded from persistence.</summary>
    /// <remarks>Exclusion wins by reference identity, even if a part is also selected by SeedEntities, returned by another aggregate, or added separately.</remarks>
    public Blueprint<TSeed, T> AsTransient() => new(Factory, Traits, Derives, true);
}
