namespace FullstackTemplate.UnitTests.Domain.Attachments;

using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.SharedTestHelpers.Fakes.Attachments;
using Shouldly;

public class FileTypeTests
{
    public static IEnumerable<Func<(FileType Type, byte[] Head)>> FilesOfEachType()
    {
        yield return () => (FileType.Png, SampleFiles.Png);
        yield return () => (FileType.Jpeg, SampleFiles.Jpeg);
        yield return () => (FileType.Gif, SampleFiles.Gif87);
        yield return () => (FileType.Gif, SampleFiles.Gif89);
        yield return () => (FileType.WebP, SampleFiles.WebP);
        yield return () => (FileType.Pdf, SampleFiles.Pdf);
    }

    [Test]
    [MethodDataSource(nameof(FilesOfEachType))]
    public void the_first_bytes_of_a_file_name_its_type_and_no_other(FileType type, byte[] head)
    {
        // Act
        var types = FileType.All.Where(t => t.StartsFile(head)).ToList();

        // Assert
        types.ShouldBe([type]);
    }

    public static IEnumerable<Func<FileType>> EachType() => FileType.All.Select(type => (Func<FileType>)(() => type));

    [Test]
    [MethodDataSource(nameof(EachType))]
    public void each_type_of_the_registry_has_a_sample_that_only_it_starts(FileType type)
    {
        // Act
        var types = FileType.All.Where(t => t.StartsFile(SampleFiles.Of(type))).ToList();

        // Assert
        types.ShouldBe([type]);
    }

    public static IEnumerable<Func<byte[]>> FilesOfNoType()
    {
        yield return () => SampleFiles.Text;
        yield return () => SampleFiles.Svg;
        yield return () => SampleFiles.Html;
        yield return () => SampleFiles.Zip;
        yield return () => [];
        // A RIFF file that is not WebP, such as a WAV sound
        yield return () => [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WAVEfmt "u8];
        // The first bytes of a PNG signature, cut short
        yield return () => SampleFiles.Png[..4];
    }

    [Test]
    [MethodDataSource(nameof(FilesOfNoType))]
    public void a_file_with_no_signature_of_the_registry_has_no_type(byte[] head)
    {
        // Act
        var types = FileType.All.Where(t => t.StartsFile(head)).ToList();

        // Assert
        types.ShouldBeEmpty();
    }

    [Test]
    public void only_a_png_jpeg_gif_or_webp_image_shows_in_the_page()
    {
        // Act
        var previewable = FileType.All.Where(t => t.CanPreview).Select(t => t.MediaType).ToList();

        // Assert
        previewable.ShouldBe(["image/png", "image/jpeg", "image/gif", "image/webp"], ignoreOrder: true);
    }

    [Test]
    [Arguments("image/svg+xml")]
    [Arguments("text/html")]
    [Arguments("application/xhtml+xml")]
    [Arguments("text/xml")]
    public void a_type_that_can_hold_a_script_is_not_in_the_registry(string mediaType)
    {
        // Act
        var type = FileType.OfMediaType(mediaType);

        // Assert
        type.ShouldBeNull();
    }

    [Test]
    public void the_first_bytes_that_an_upload_reads_hold_each_signature()
    {
        // Arrange
        var samples = FilesOfEachType().Select(f => f()).ToList();

        // Act
        var cut = samples.Where(s => !s.Type.StartsFile(s.Head.AsSpan(0, Math.Min(s.Head.Length, FileType.HeadLength)))).ToList();

        // Assert
        cut.ShouldBeEmpty();
    }

    [Test]
    [Arguments("kiln.jpg", true)]
    [Arguments("kiln.jpeg", true)]
    [Arguments("KILN.JPG", true)]
    [Arguments("kiln.png.jpg", true)]
    [Arguments("kiln.jpg.png", false)]
    [Arguments("kiln", false)]
    [Arguments("jpg", false)]
    public void a_name_names_a_file_of_the_type_by_one_of_its_extensions(string fileName, bool names)
    {
        // Act
        var result = FileType.Jpeg.NamesFile(fileName);

        // Assert
        result.ShouldBe(names);
    }
}
