namespace FullstackTemplate.UnitTests.Domain.Attachments;

using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.Fakes.Attachments;
using Shouldly;

public class UploadRulesTests
{
    private static readonly UploadRules ImagesAndPdf = new(10, FileType.Png, FileType.Jpeg, FileType.Gif, FileType.WebP, FileType.Pdf);

    private static readonly UploadRules SmallPdf = new(2, FileType.Pdf);

    [Test]
    public void a_file_of_exactly_the_largest_size_is_taken_and_one_byte_more_is_refused_with_the_size_of_the_rules()
    {
        // Arrange
        var largest = SampleFiles.Upload("handout.pdf", SampleFiles.Pdf, sizeBytes: 2 * 1024 * 1024);
        var tooLarge = largest with { SizeBytes = 2 * 1024 * 1024 + 1 };

        // Act
        var taken = SmallPdf.TypeOf(largest);
        var refuse = () => SmallPdf.TypeOf(tooLarge);

        // Assert
        taken.ShouldBe(FileType.Pdf);
        var refusal = refuse.ShouldThrow<ValidationException>();
        refusal.Property.ShouldBe("sizeBytes");
        refusal.Message.ShouldBe("A file can be no larger than 2 MB. Choose a smaller file.");
    }

    [Test]
    public void the_message_of_a_large_file_names_the_size_of_its_own_rules()
    {
        // Act
        var message = ImagesAndPdf.FileTooLargeMessage;

        // Assert
        message.ShouldBe("A file can be no larger than 10 MB. Choose a smaller file.");
        ImagesAndPdf.MaxSizeBytes.ShouldBe(10 * 1024 * 1024);
    }

    [Test]
    public void an_empty_file_is_refused()
    {
        // Arrange
        var empty = SampleFiles.Upload("handout.pdf", [], sizeBytes: 0);

        // Act
        var refuse = () => ImagesAndPdf.TypeOf(empty);

        // Assert
        var refusal = refuse.ShouldThrow<ValidationException>();
        refusal.Property.ShouldBe("sizeBytes");
        refusal.Message.ShouldBe("The file is empty. Choose a file that has content.");
    }

    [Test]
    [Arguments("kiln.png", "image/png")]
    [Arguments("kiln.PNG", "image/png")]
    [Arguments("kiln.png", "IMAGE/PNG")]
    [Arguments("kiln.png", "image/x-png")]
    [Arguments("kiln.png", null)]
    [Arguments("kiln.png", "")]
    [Arguments("kiln.png", "application/octet-stream")]
    public void a_png_with_its_extension_is_a_png_when_the_client_gives_its_type_or_no_type(string fileName, string? claimed)
    {
        // Arrange
        var upload = SampleFiles.Upload(fileName, SampleFiles.Png, claimed);

        // Act
        var type = ImagesAndPdf.TypeOf(upload);

        // Assert
        type.ShouldBe(FileType.Png);
    }

    [Test]
    public void a_png_renamed_to_pdf_is_refused_with_the_extension_of_its_type()
    {
        // Arrange
        var upload = SampleFiles.Upload("kiln.pdf", SampleFiles.Png, "application/pdf");

        // Act
        var refuse = () => ImagesAndPdf.TypeOf(upload);

        // Assert
        var refusal = refuse.ShouldThrow<ValidationException>();
        refusal.Property.ShouldBe("fileName");
        refusal.Message.ShouldBe("The file is a PNG image, so its name must end in .png. Rename the file, then upload it again.");
    }

    [Test]
    public void a_jpeg_with_no_extension_is_refused_with_both_extensions_of_its_type()
    {
        // Arrange
        var upload = SampleFiles.Upload("kiln", SampleFiles.Jpeg);

        // Act
        var refuse = () => ImagesAndPdf.TypeOf(upload);

        // Assert
        refuse.ShouldThrow<ValidationException>().Message
            .ShouldBe("The file is a JPEG image, so its name must end in .jpg or .jpeg. Rename the file, then upload it again.");
    }

    [Test]
    [Arguments("application/pdf")]
    [Arguments("text/html")]
    [Arguments("not a type")]
    [Arguments("image/png\r\nX-Evil: 1")]
    public void a_png_that_the_client_sends_as_another_type_is_refused(string claimed)
    {
        // Arrange
        var upload = SampleFiles.Upload("kiln.png", SampleFiles.Png, claimed);

        // Act
        var refuse = () => ImagesAndPdf.TypeOf(upload);

        // Assert
        var refusal = refuse.ShouldThrow<ValidationException>();
        refusal.Property.ShouldBe("mediaType");
        refusal.Message.ShouldBe("The type that the upload gives does not match the file, which is a PNG image. Upload the file again.");
    }

    public static IEnumerable<Func<(string FileName, byte[] Head, string Claimed)>> FilesOfNoAllowedType()
    {
        yield return () => ("notes.png", SampleFiles.Text, "image/png");
        yield return () => ("notes.txt", SampleFiles.Text, "text/plain");
        yield return () => ("logo.svg", SampleFiles.Svg, "image/svg+xml");
        yield return () => ("page.html", SampleFiles.Html, "text/html");
        yield return () => ("logo.png", SampleFiles.Svg, "image/png");
        yield return () => ("sheet.xlsx", SampleFiles.Zip, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Test]
    [MethodDataSource(nameof(FilesOfNoAllowedType))]
    public void a_file_whose_bytes_are_no_allowed_type_is_refused_with_the_types_of_the_rules(string fileName, byte[] head, string claimed)
    {
        // Arrange
        var upload = SampleFiles.Upload(fileName, head, claimed);

        // Act
        var refuse = () => ImagesAndPdf.TypeOf(upload);

        // Assert
        var refusal = refuse.ShouldThrow<ValidationException>();
        refusal.Property.ShouldBe("mediaType");
        refusal.Message.ShouldBe("The file type must be PNG, JPEG, GIF, WebP, or PDF. Choose another file.");
    }

    [Test]
    public void a_type_of_the_registry_that_the_rules_do_not_allow_is_refused()
    {
        // Arrange
        var upload = SampleFiles.Upload("kiln.png", SampleFiles.Png, "image/png");

        // Act
        var refuse = () => SmallPdf.TypeOf(upload);

        // Assert
        refuse.ShouldThrow<ValidationException>().Message.ShouldBe("The file type must be PDF. Choose another file.");
    }

    [Test]
    public void the_message_of_two_types_joins_them_with_or()
    {
        // Arrange
        var rules = new UploadRules(1, FileType.Png, FileType.Pdf);

        // Act
        var message = rules.TypeNotAllowedMessage;

        // Assert
        message.ShouldBe("The file type must be PNG or PDF. Choose another file.");
    }

    [Test]
    public void a_large_file_gets_the_size_message_before_its_type_is_checked()
    {
        // Arrange
        var upload = SampleFiles.Upload("video.mp4", SampleFiles.Text, "video/mp4", sizeBytes: 11 * 1024 * 1024);

        // Act
        var refuse = () => ImagesAndPdf.TypeOf(upload);

        // Assert
        refuse.ShouldThrow<ValidationException>().Property.ShouldBe("sizeBytes");
    }

    [Test]
    [Arguments(0)]
    [Arguments(2048)]
    public void rules_refuse_a_largest_size_that_the_client_cannot_read_as_an_int(int maxSizeMegabytes)
    {
        // Act
        var make = () => new UploadRules(maxSizeMegabytes, FileType.Pdf);

        // Assert
        make.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Test]
    public void rules_need_at_least_one_type()
    {
        // Act
        var make = () => new UploadRules(1);

        // Assert
        make.ShouldThrow<ArgumentException>();
    }
}
