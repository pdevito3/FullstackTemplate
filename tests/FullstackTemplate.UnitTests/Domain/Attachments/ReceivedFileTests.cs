namespace FullstackTemplate.UnitTests.Domain.Attachments;

using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.SharedTestHelpers.Fakes.Attachments;
using Shouldly;

public class ReceivedFileTests
{
    [Test]
    public async Task the_size_is_the_length_of_the_stream()
    {
        // Arrange
        var bytes = SampleFiles.OfLength(SampleFiles.Pdf, 3000);

        // Act
        await using var file = new ReceivedFile(new MemoryStream(bytes), "plan.pdf", "application/pdf");

        // Assert
        file.SizeBytes.ShouldBe(3000);
    }

    [Test]
    public async Task a_stream_that_cannot_seek_is_refused()
    {
        // Arrange
        await using var content = new ForwardOnlyStream(SampleFiles.Png);

        // Act
        var make = () => new ReceivedFile(content, "kiln.png", "image/png");

        // Assert
        make.ShouldThrow<ArgumentException>().ParamName.ShouldBe("content");
    }

    [Test]
    public async Task a_dispose_disposes_the_stream()
    {
        // Arrange
        var content = new MemoryStream(SampleFiles.Png);
        var file = new ReceivedFile(content, "kiln.png", "image/png");

        // Act
        await file.DisposeAsync();

        // Assert
        content.CanRead.ShouldBeFalse();
    }

    private sealed class ForwardOnlyStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
