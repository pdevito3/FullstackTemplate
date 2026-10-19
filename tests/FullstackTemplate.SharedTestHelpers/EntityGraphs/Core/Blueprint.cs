namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>Defines explicit factories for entities, children, and input-based outputs.</summary>
/// <remarks>Entities, parents, and inputs must be reference types so mutation and graph identity remain stable. Seeds may have any shape.</remarks>
public static class Blueprint
{
    /// <summary>Defines an entity factory.</summary>
    public static Blueprint<TSeed, T> For<TSeed, T>(Func<GraphContext<TSeed>, T> factory)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(factory, [], [], false);
    }

    /// <summary>Defines a child factory requiring a typed parent anchor.</summary>
    public static ChildBlueprint<TSeed, TParent, T> ChildOf<TSeed, TParent, T>(
        Func<TParent, ChildContext<TSeed, TParent>, T> factory
    )
        where TParent : class
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(factory, [], [], false);
    }

    /// <summary>Defines an output factory whose customizers operate on its input.</summary>
    public static InputBlueprint<TSeed, TInput, T> FromInput<TSeed, TInput, T>(
        Func<TInput> newInput,
        Func<TInput, GraphContext<TSeed>, T> build
    )
        where TInput : class
        where T : class
    {
        ArgumentNullException.ThrowIfNull(newInput);
        ArgumentNullException.ThrowIfNull(build);
        return new(newInput, build, [], [], false);
    }

    /// <summary>Defines an input-based child factory requiring a typed parent anchor.</summary>
    public static ChildInputBlueprint<TSeed, TParent, TInput, T> ChildFromInput<
        TSeed,
        TParent,
        TInput,
        T
    >(Func<TInput> newInput, Func<TParent, TInput, ChildContext<TSeed, TParent>, T> build)
        where TParent : class
        where TInput : class
        where T : class
    {
        ArgumentNullException.ThrowIfNull(newInput);
        ArgumentNullException.ThrowIfNull(build);
        return new(newInput, build, [], [], false);
    }
}
