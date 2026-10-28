namespace FullstackTemplate.SharedTestHelpers.EntityGraphs;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>
/// The entry point for test scenarios. The builder starts anchored to the seed tenant,
/// so <c>AddUser</c> right after <c>Start</c> creates a user in the tenant of the current user.
/// </summary>
public static class TestGraph
{
    /// <summary>
    /// The seed rows are owned, so they can be parents for <c>Under</c>. The test fixture inserts the seed
    /// once per run, so the save hook takes the seed rows out and a build saves only the rows that the test adds.
    /// The graph makes IDs with <see cref="EntityId.New"/>, the same generator as the domain.
    /// </summary>
    private static readonly GraphOptions<TestSeed> Options = new GraphOptions<TestSeed>()
        .Ids(EntityId.New)
        .SeedEntities(seed => [seed.Tenant, seed.User])
        .BeforeSave((ctx, _) =>
        {
            ctx.Entities.RemoveAll(e => ReferenceEquals(e, ctx.Seed.Tenant) || ReferenceEquals(e, ctx.Seed.User));
            return ValueTask.CompletedTask;
        });

    /// <summary>For unit tests. Generates a new in-memory seed. <c>BuildAsync</c> saves nothing.</summary>
    public static GraphBuilder<TestSeed, Tenant[]> Start(out TestSeed seed, TimeProvider? time = null)
    {
        seed = TestSeed.Generate();
        return Start(seed, NullStore.Instance, time);
    }

    /// <summary>
    /// For tests with a database. The seed rows must already exist. <c>BuildAsync</c> does not save them.
    /// Without <paramref name="time"/>, <c>ctx.Now</c> is frozen at start.
    /// </summary>
    public static GraphBuilder<TestSeed, Tenant[]> Start(TestSeed seed, IGraphStore store, TimeProvider? time = null)
        => EntityGraph.Start(seed, Options, store, time).Under(seed.Tenant);
}
