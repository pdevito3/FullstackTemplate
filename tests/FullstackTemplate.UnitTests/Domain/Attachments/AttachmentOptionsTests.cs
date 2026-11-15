namespace FullstackTemplate.UnitTests.Domain.Attachments;

using FullstackTemplate.Server.Domain.Attachments;
using Microsoft.Extensions.Configuration;
using Shouldly;

public class AttachmentOptionsTests
{
    [Test]
    public void no_settings_give_an_add_five_minutes()
    {
        // Act
        var options = AttachmentOptions.From(new ConfigurationBuilder().Build());

        // Assert
        options.AddTimeout.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Test]
    [Arguments("00:00:00")]
    [Arguments("-00:00:01")]
    public void an_add_timeout_that_is_not_over_zero_stops_the_start(string value)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{AttachmentOptions.SectionName}:AddTimeout"] = value })
            .Build();

        // Act
        var act = () => AttachmentOptions.From(configuration);

        // Assert
        act.ShouldThrow<InvalidOperationException>();
    }
}
