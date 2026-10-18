namespace FullstackTemplate.AppHost;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Support for <c>aspire run --isolated</c>, so that worktrees can run the full stack at the same time.
/// </summary>
public static class Isolation
{
    // `aspire run --isolated` sets DcpPublisher__RandomizePorts=true on the AppHost process.
    private const string RandomizePortsKey = "DcpPublisher:RandomizePorts";

    /// <summary>
    /// Returns true when the AppHost runs with <c>aspire run --isolated</c> (or <c>aspire start --isolated</c>).
    /// </summary>
    public static bool IsIsolated(this IDistributedApplicationBuilder builder)
        => builder.Configuration.GetValue<bool>(RandomizePortsKey);

    /// <summary>
    /// Adds a data volume in a normal run. The default volume name has a hash of the AppHost path,
    /// so each worktree gets its own data. An isolated run gets no volume and starts with empty data,
    /// so two isolated runs of one checkout do not share a data directory.
    /// </summary>
    public static IResourceBuilder<PostgresServerResource> WithDataVolumeUnlessIsolated(
        this IResourceBuilder<PostgresServerResource> postgres)
        => postgres.ApplicationBuilder.IsIsolated() ? postgres : postgres.WithDataVolume();

    /// <inheritdoc cref="WithDataVolumeUnlessIsolated(IResourceBuilder{PostgresServerResource})"/>
    public static IResourceBuilder<ContainerResource> WithDataVolumeUnlessIsolated(
        this IResourceBuilder<ContainerResource> container, string target)
        => container.ApplicationBuilder.IsIsolated()
            ? container
            : container.WithVolume(VolumeNameGenerator.Generate(container, "data"), target);
}
