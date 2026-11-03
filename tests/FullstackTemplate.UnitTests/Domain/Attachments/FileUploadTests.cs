namespace FullstackTemplate.UnitTests.Domain.Attachments;

using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.SharedTestHelpers.Fakes.Attachments;
using Shouldly;

public class FileUploadTests
{
    [Test]
    public async Task an_upload_reads_only_the_first_bytes_and_puts_the_stream_back_at_its_start()
    {
        // Arrange
        var bytes = SampleFiles.OfLength(SampleFiles.Png, 4096);
        await using var content = new MemoryStream(bytes);

        // Act
        var upload = await FileUpload.ReadAsync(content, "kiln.png", "image/png", bytes.Length, CancellationToken.None);

        // Assert
        upload.Head.ToArray().ShouldBe(bytes[..FileType.HeadLength]);
        upload.SizeBytes.ShouldBe(4096);
        content.Position.ShouldBe(0);
        content.ToArray().ShouldBe(bytes);
    }

    [Test]
    public async Task a_file_shorter_than_the_head_gives_all_of_its_bytes()
    {
        // Arrange
        byte[] bytes = [0x25, 0x50, 0x44];
        await using var content = new MemoryStream(bytes);

        // Act
        var upload = await FileUpload.ReadAsync(content, "a.pdf", null, bytes.Length, CancellationToken.None);

        // Assert
        upload.Head.ToArray().ShouldBe(bytes);
        content.Position.ShouldBe(0);
    }

    [Test]
    public async Task an_upload_needs_a_stream_that_can_seek()
    {
        // Arrange
        await using var content = new ForwardOnlyStream(SampleFiles.Png);

        // Act
        var read = () => FileUpload.ReadAsync(content, "kiln.png", "image/png", SampleFiles.Png.Length, CancellationToken.None);

        // Assert
        await read.ShouldThrowAsync<ArgumentException>();
    }

    private sealed class ForwardOnlyStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
