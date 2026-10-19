namespace FullstackTemplate.UnitTests.AppHost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using FullstackTemplate.AppHost;
using Shouldly;

public class IsolationTests
{
    private const string IsolatedArgument = "--DcpPublisher:RandomizePorts=true";

    [Test]
    public void a_normal_run_is_not_isolated()
    {
        // Act
        var builder = CreateBuilder();

        // Assert
        builder.IsIsolated().ShouldBeFalse();
    }

    [Test]
    public void the_isolated_flag_of_the_aspire_cli_makes_the_run_isolated()
    {
        // Act
        var builder = CreateBuilder(IsolatedArgument);

        // Assert
        builder.IsIsolated().ShouldBeTrue();
    }

    [Test]
    public void a_normal_run_gives_postgres_a_volume_named_from_the_apphost_path()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var postgres = builder.AddPostgres("postgres").WithDataVolumeUnlessIsolated();

        // Assert
        var volume = postgres.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        volume.Type.ShouldBe(ContainerMountType.Volume);
        volume.Source.ShouldBe(VolumeNameGenerator.Generate(postgres, "data"));
    }

    [Test]
    public void an_isolated_run_gives_postgres_no_volume()
    {
        // Arrange
        var builder = CreateBuilder(IsolatedArgument);

        // Act
        var postgres = builder.AddPostgres("postgres").WithDataVolumeUnlessIsolated();

        // Assert
        postgres.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldBeEmpty();
    }

    [Test]
    public void a_normal_run_gives_a_container_a_volume_at_the_target()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var redis = builder.AddContainer("redis", "redis").WithDataVolumeUnlessIsolated("/data");

        // Assert
        var volume = redis.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();
        volume.Target.ShouldBe("/data");
        volume.Source.ShouldBe(VolumeNameGenerator.Generate(redis, "data"));
    }

    [Test]
    public void an_isolated_run_gives_a_container_no_volume()
    {
        // Arrange
        var builder = CreateBuilder(IsolatedArgument);

        // Act
        var redis = builder.AddContainer("redis", "redis").WithDataVolumeUnlessIsolated("/data");

        // Assert
        redis.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldBeEmpty();
    }

    private static IDistributedApplicationBuilder CreateBuilder(params string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = args, DisableDashboard = true });

        // A test host that `aspire run --isolated` starts (for example the Labcoat resource) inherits
        // DcpPublisher__RandomizePorts=true. A normal run has no value for the key, so remove the inherited value.
        if (args.Length == 0)
            builder.Configuration["DcpPublisher:RandomizePorts"] = null;

        return builder;
    }
}
