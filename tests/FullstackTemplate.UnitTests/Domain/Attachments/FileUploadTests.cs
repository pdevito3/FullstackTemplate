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
        var content = new MemoryStream(bytes);
        await using var file = new ReceivedFile(content, "kiln.png", "image/png");

        // Act
        var upload = await FileUpload.ReadAsync(file, CancellationToken.None);

        // Assert
        upload.FileName.ShouldBe("kiln.png");
        upload.ClaimedMediaType.ShouldBe("image/png");
        upload.Head.ToArray().ShouldBe(bytes[..FileType.HeadLength]);
        upload.SizeBytes.ShouldBe(4096);
        content.Position.ShouldBe(0);
        content.ToArray().ShouldBe(bytes);
    }

    [Test]
    public async Task an_upload_reads_from_the_start_when_the_stream_is_not_at_its_start()
    {
        // Arrange
        var bytes = SampleFiles.OfLength(SampleFiles.Png, 4096);
        var content = new MemoryStream(bytes) { Position = 100 };
        await using var file = new ReceivedFile(content, "kiln.png", "image/png");

        // Act
        var upload = await FileUpload.ReadAsync(file, CancellationToken.None);

        // Assert
        upload.Head.ToArray().ShouldBe(bytes[..FileType.HeadLength]);
        content.Position.ShouldBe(0);
    }

    [Test]
    public async Task a_file_shorter_than_the_head_gives_all_of_its_bytes()
    {
        // Arrange
        byte[] bytes = [0x25, 0x50, 0x44];
        var content = new MemoryStream(bytes);
        await using var file = new ReceivedFile(content, "a.pdf", null);

        // Act
        var upload = await FileUpload.ReadAsync(file, CancellationToken.None);

        // Assert
        upload.Head.ToArray().ShouldBe(bytes);
        content.Position.ShouldBe(0);
    }
}
