namespace FullstackTemplate.UnitTests.Domain.ObjectDeletes;

using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.Server.Domain.ObjectDeletes;
using Microsoft.Extensions.Configuration;
using Shouldly;

public class ObjectSweepOptionsTests
{
    [Test]
    public void no_settings_sweep_each_day_at_three_and_keep_an_object_for_the_add_timeout_and_one_hour()
    {
        // Act
        var options = ObjectSweepOptions.From(new ConfigurationBuilder().Build());

        // Assert
        options.Cron.Canonical.ShouldBe("0 0 3 * * *");
        options.GraceFor(new AttachmentOptions()).ShouldBe(TimeSpan.FromMinutes(65));
    }

    [Test]
    [Arguments("00:15:00")]
    [Arguments("00:00:00")]
    public void a_margin_not_over_the_largest_skew_of_the_clocks_stops_the_start(string margin)
    {
        // Act
        var act = () => ObjectSweepOptions.From(Configuration("Margin", margin));

        // Assert
        act.ShouldThrow<InvalidOperationException>();
    }

    [Test]
    [Arguments("each night")]
    [Arguments("0 3 * *")]
    [Arguments("0 25 * * *")]
    public void a_schedule_that_is_not_a_cron_stops_the_start(string schedule)
    {
        // Act
        var act = () => ObjectSweepOptions.From(Configuration("Schedule", schedule));

        // Assert
        act.ShouldThrow<InvalidOperationException>();
    }

    private static IConfiguration Configuration(string name, string value)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{ObjectSweepOptions.SectionName}:{name}"] = value })
            .Build();
}
