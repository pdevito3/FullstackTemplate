namespace FullstackTemplate.UnitTests.Domain.Attachments;

using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.Fakes.Attachments;
using Shouldly;

public class StoredFileTests
{
    private const string Key = "tenants/t1/things/a1/attachments/f1";

    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private static readonly UploadRules ImagesAndPdf = new(8, FileType.Png, FileType.Jpeg, FileType.Gif, FileType.WebP, FileType.Pdf);

    [Test]
    public void an_accepted_file_keeps_the_name_the_size_and_the_key_and_the_type_of_its_bytes()
    {
        // Arrange
        var upload = SampleFiles.Upload("kiln.png", SampleFiles.Png, "image/x-png", sizeBytes: 2048);

        // Act
        var file = StoredFile.Accept(upload, ImagesAndPdf, Key);

        // Assert
        file.Name.ShouldBe("kiln.png");
        file.MediaType.ShouldBe("image/png");
        file.SizeBytes.ShouldBe(2048);
        file.StorageKey.ShouldBe(Key);
    }

    [Test]
    public void a_file_sent_as_plain_bytes_is_stored_with_the_type_of_its_bytes()
    {
        // Arrange
        var upload = SampleFiles.Upload("handout.pdf", SampleFiles.Pdf, "application/octet-stream");

        // Act
        var file = StoredFile.Accept(upload, ImagesAndPdf, Key);

        // Assert
        file.MediaType.ShouldBe("application/pdf");
    }

    public static IEnumerable<Func<(string FileName, string Message)>> InvalidNames()
    {
        yield return () => (" ", "The file has no name. Rename the file, then upload it again.");
        yield return () => (new string('a', StoredFile.NameMaxLength - 3) + ".png",
            "A file name can have no more than 255 characters. Rename the file, then upload it again.");
        yield return () => ("a\0b.png", "The file name has a character that is not allowed. Rename the file, then upload it again.");
        yield return () => ("../kiln.png", "A file name cannot have a slash or a backslash. Rename the file, then upload it again.");
        yield return () => (@"C:\kiln.png", "A file name cannot have a slash or a backslash. Rename the file, then upload it again.");
    }

    [Test]
    [MethodDataSource(nameof(InvalidNames))]
    public void a_file_refuses_an_invalid_name(string fileName, string message)
    {
        // Arrange
        var upload = SampleFiles.Upload(fileName, SampleFiles.Png, "image/png");

        // Act
        var accept = () => StoredFile.Accept(upload, ImagesAndPdf, Key);

        // Assert
        var refusal = accept.ShouldThrow<ValidationException>();
        refusal.Property.ShouldBe("fileName");
        refusal.Message.ShouldBe(message);
    }

    [Test]
    public void a_name_of_exactly_the_longest_length_is_taken()
    {
        // Arrange
        var upload = SampleFiles.Upload(new string('a', StoredFile.NameMaxLength - 4) + ".png", SampleFiles.Png);

        // Act
        var file = StoredFile.Accept(upload, ImagesAndPdf, Key);

        // Assert
        file.Name.Length.ShouldBe(StoredFile.NameMaxLength);
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    public void a_file_needs_a_key(string storageKey)
    {
        // Arrange
        var upload = SampleFiles.Upload("kiln.png", SampleFiles.Png);

        // Act
        var accept = () => StoredFile.Accept(upload, ImagesAndPdf, storageKey);

        // Assert
        accept.ShouldThrow<ArgumentException>();
    }

    [Test]
    public void each_media_type_of_the_registry_fits_the_column()
    {
        // Act
        var tooLong = FileType.All.Where(t => t.MediaType.Length > StoredFile.MediaTypeMaxLength).ToList();

        // Assert
        tooLong.ShouldBeEmpty();
    }

    public static IEnumerable<Func<(string FileName, byte[] Head, bool CanPreview)>> FilesOfEachType()
    {
        yield return () => ("kiln.png", SampleFiles.Png, true);
        yield return () => ("kiln.jpg", SampleFiles.Jpeg, true);
        yield return () => ("kiln.gif", SampleFiles.Gif89, true);
        yield return () => ("kiln.webp", SampleFiles.WebP, true);
        yield return () => ("handout.pdf", SampleFiles.Pdf, false);
    }

    [Test]
    [MethodDataSource(nameof(FilesOfEachType))]
    public void only_an_image_that_the_registry_can_preview_gets_a_preview(string fileName, byte[] head, bool canPreview)
    {
        // Arrange
        var file = StoredFile.Accept(SampleFiles.Upload(fileName, head), ImagesAndPdf, Key);

        // Act
        var preview = file.PreviewAt(Now);

        // Assert
        file.CanPreview.ShouldBe(canPreview);
        (preview is not null).ShouldBe(canPreview);
    }

    [Test]
    public void the_download_is_an_attachment_with_the_encoded_name_and_the_stored_type_for_15_minutes()
    {
        // Arrange
        var file = StoredFile.Accept(SampleFiles.Upload("résumé \"final\"; v2.png", SampleFiles.Png), ImagesAndPdf, Key);

        // Act
        var download = file.DownloadAt(Now);

        // Assert
        download.Key.ShouldBe(Key);
        download.ContentType.ShouldBe("image/png");
        download.ContentDisposition.ShouldBe("attachment; filename*=UTF-8''r%C3%A9sum%C3%A9%20%22final%22%3B%20v2.png");
        download.ExpiresAt.ShouldBe(Now + TimeSpan.FromMinutes(15));
    }

    [Test]
    public void the_preview_is_inline_with_the_stored_type_and_the_same_expiry()
    {
        // Arrange
        var file = StoredFile.Accept(SampleFiles.Upload("kiln.webp", SampleFiles.WebP, "image/webp"), ImagesAndPdf, Key);

        // Act
        var preview = file.PreviewAt(Now).ShouldNotBeNull();

        // Assert
        preview.Key.ShouldBe(Key);
        preview.ContentType.ShouldBe("image/webp");
        preview.ContentDisposition.ShouldBe("inline; filename*=UTF-8''kiln.webp");
        preview.ExpiresAt.ShouldBe(file.DownloadAt(Now).ExpiresAt);
    }

    [Test]
    public void two_files_with_the_same_values_are_equal()
    {
        // Arrange
        var upload = SampleFiles.Upload("kiln.png", SampleFiles.Png);

        // Act
        var first = StoredFile.Accept(upload, ImagesAndPdf, Key);
        var second = StoredFile.Accept(upload, ImagesAndPdf, Key);

        // Assert
        first.ShouldBe(second);
        first.ShouldNotBe(StoredFile.Accept(upload, ImagesAndPdf, Key + "-other"));
    }
}
