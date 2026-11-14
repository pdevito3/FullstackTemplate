namespace FullstackTemplate.UnitTests.Domain.ObjectDeletes;

using BackWave.Core;
using BackWave.Generated;
using FullstackTemplate.Server.Domain.ObjectDeletes;
using FullstackTemplate.Server.Resources.Jobs;
using Microsoft.Extensions.Configuration;
using Shouldly;

public class ObjectDeleteOptionsTests
{
    [Test]
    public void the_delay_after_a_failed_attempt_doubles_up_to_the_largest_delay()
    {
        // Arrange
        var options = new ObjectDeleteOptions { FirstRetryDelay = TimeSpan.FromSeconds(30), MaxRetryDelay = TimeSpan.FromMinutes(3) };

        // Act
        var delays = Enumerable.Range(1, 5).Select(options.RetryDelayAfter);

        // Assert
        delays.ShouldBe([TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120),
            TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(3)]);
    }

    [Test]
    public void the_delay_after_the_last_allowed_attempt_does_not_overflow()
    {
        // Arrange
        var options = new ObjectDeleteOptions { MaxRetryDelay = TimeSpan.MaxValue };

        // Act
        var act = () => options.RetryDelayAfter(RetryDisposition.MaxAttemptCeiling);

        // Assert
        act.ShouldNotThrow().ShouldBe(TimeSpan.MaxValue);
    }

    [Test]
    public void the_worker_group_serves_only_the_object_deletes_with_the_retry_of_the_settings()
    {
        // Arrange
        var options = new ObjectDeleteOptions { MaxAttempts = 4 };

        // Act
        var group = options.WorkerGroup();

        // Assert
        group.Name.ShouldBe(ObjectDeleteOptions.Queue);
        group.Policy.ShouldBeOfType<DispatchPolicy.Strict>().Queues.ShouldBe([ObjectDeleteOptions.Queue]);
        group.RetryPolicy.MaxAttempts.ShouldBe(4);
        group.RetryPolicy.Backoff(2).ShouldBe(options.RetryDelayAfter(2));
    }

    [Test]
    public void each_queue_of_the_jobs_gets_one_worker_group_and_the_object_deletes_get_their_retry()
    {
        // Arrange
        var options = new ObjectDeleteOptions { MaxAttempts = 4 };

        // Act
        var groups = JobGroups.For(BackWaveJobs.Module, options).ToList();

        // Assert
        groups.Select(g => g.Name).ShouldBe(
            BackWaveJobs.Module.Registrations.Select(r => r.Queue).Distinct().Order(StringComparer.Ordinal));
        groups.Single(g => g.Name == ObjectDeleteOptions.Queue).RetryPolicy.MaxAttempts.ShouldBe(4);
    }

    [Test]
    public void no_settings_give_the_defaults()
    {
        // Act
        var options = ObjectDeleteOptions.From(new ConfigurationBuilder().Build());

        // Assert
        options.ShouldBe(new ObjectDeleteOptions());
    }

    [Test]
    [Arguments("FirstRetryDelay", "00:00:00")]
    [Arguments("MaxRetryDelay", "00:00:01")]
    [Arguments("AttemptTimeout", "00:00:00")]
    [Arguments("MaxAttempts", "0")]
    [Arguments("MaxAttempts", "1001")]
    public void a_setting_out_of_its_range_stops_the_start(string setting, string value)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{ObjectDeleteOptions.SectionName}:{setting}"] = value })
            .Build();

        // Act
        var act = () => ObjectDeleteOptions.From(configuration);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe(
            "ObjectStorage:Deletes needs times over zero, a MaxRetryDelay not under FirstRetryDelay, and MaxAttempts from 1 to 1000.");
    }
}
