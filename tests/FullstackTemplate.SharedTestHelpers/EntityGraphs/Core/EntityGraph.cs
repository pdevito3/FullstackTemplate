namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>Starts eager, typed test scenarios.</summary>
public static class EntityGraph
{
    /// <summary>Starts a session anchored to its seed and freezes the supplied options.</summary>
    /// <remarks>With no time provider, time is frozen at start. A supplied provider is read live. Both are precision-rounded.</remarks>
    public static GraphBuilder<TSeed, TSeed> Start<TSeed>(
        TSeed seed,
        GraphOptions<TSeed> options,
        IGraphStore? store = null,
        TimeProvider? time = null
    )
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(options);
        options.Freeze();
        return new(new(seed, options, store ?? NullStore.Instance, time), seed);
    }
}
