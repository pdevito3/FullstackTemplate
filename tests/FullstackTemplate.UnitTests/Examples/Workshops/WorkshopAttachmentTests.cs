namespace FullstackTemplate.UnitTests.Examples.Workshops;

using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.Server.Examples.Workshops.DomainEvents;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using Shouldly;

public class WorkshopAttachmentTests
{
    [Test]
    public void a_new_attachment_is_in_the_tenant_and_the_workshop_and_its_key_names_them()
    {
        // Act
        TestGraph.Start(out var seed, Clock.Fixed)
            .AddVenue(out _)
            .AddWorkshop(out var workshop)
            .AddWorkshopAttachment(out var attachment);

        // Assert
        attachment.TenantId.ShouldBe(seed.Tenant.Id);
        attachment.WorkshopId.ShouldBe(workshop.Id);
        attachment.StorageKey.ShouldBe($"tenants/{seed.Tenant.Id}/workshops/{workshop.Id}/attachments/{attachment.Id}");
        attachment.DomainEvents.ShouldBe([new WorkshopAttachmentAdded(attachment.Id, workshop.Id)]);
    }

    public static IEnumerable<Func<(Action<WorkshopAttachmentInput> Customize, string Field, string Message)>> InvalidInputs()
    {
        yield return () => (a => a.FileName = " ", "fileName", "The file has no name. Rename the file, then upload it again.");
        yield return () => (a => a.FileName = new string('a', WorkshopAttachment.FileNameMaxLength + 1), "fileName",
            "A file name can have no more than 255 characters. Rename the file, then upload it again.");
        yield return () => (a => a.FileName = "a\0b.png", "fileName",
            "The file name has a character that is not allowed. Rename the file, then upload it again.");
        yield return () => (a => a.FileName = "../notes.pdf", "fileName",
            "A file name cannot have a slash or a backslash. Rename the file, then upload it again.");
        yield return () => (a => a.FileName = @"C:\notes.pdf", "fileName",
            "A file name cannot have a slash or a backslash. Rename the file, then upload it again.");
        yield return () => (a => a.SizeBytes = 0, "sizeBytes", "The file is empty. Choose a file that has content.");
        yield return () => (a => a.SizeBytes = WorkshopAttachment.SizeBytesMaximum + 1, "sizeBytes",
            "A file can be no larger than 10 MB. Choose a smaller file.");
    }

    [Test]
    [MethodDataSource(nameof(InvalidInputs))]
    public void an_attachment_refuses_an_invalid_value(Action<WorkshopAttachmentInput> customize, string field, string message)
    {
        // Arrange
        var graph = TestGraph.Start(out _, Clock.Fixed).AddVenue(out _).AddWorkshop(out _);

        // Act
        var act = () => graph.AddWorkshopAttachment(out _, customize);

        // Assert
        var refusal = act.ShouldThrow<Exception>().GetBaseException().ShouldBeOfType<ValidationException>();
        refusal.Property.ShouldBe(field);
        refusal.Message.ShouldBe(message);
    }

    [Test]
    public void a_file_of_exactly_10_megabytes_is_taken()
    {
        // Act
        TestGraph.Start(out _, Clock.Fixed).AddVenue(out _).AddWorkshop(out _)
            .AddWorkshopAttachment(out var attachment, a => a.SizeBytes = 10 * 1024 * 1024);

        // Assert
        attachment.SizeBytes.ShouldBe(WorkshopAttachment.SizeBytesMaximum);
    }

    [Test]
    [Arguments("", "application/octet-stream")]
    [Arguments("  ", "application/octet-stream")]
    [Arguments("not a type", "application/octet-stream")]
    [Arguments("image/png\r\nX-Evil: 1", "application/octet-stream")]
    [Arguments("text/plain; charset=\u00e9", "application/octet-stream")]
    [Arguments(" application/pdf ", "application/pdf")]
    [Arguments("text/plain; charset=utf-8", "text/plain; charset=utf-8")]
    public void the_type_of_the_client_is_kept_only_when_it_is_a_valid_media_type(string sent, string stored)
    {
        // Act
        TestGraph.Start(out _, Clock.Fixed).AddVenue(out _).AddWorkshop(out _)
            .AddWorkshopAttachment(out var attachment, a => a.ContentType = sent);

        // Assert
        attachment.ContentType.ShouldBe(stored);
    }

    [Test]
    public void a_type_that_does_not_fit_the_column_is_stored_as_plain_bytes()
    {
        // Arrange
        var tooLong = "application/" + new string('x', WorkshopAttachment.ContentTypeMaxLength);

        // Act
        TestGraph.Start(out _, Clock.Fixed).AddVenue(out _).AddWorkshop(out _)
            .AddWorkshopAttachment(out var attachment, a => a.ContentType = tooLong);

        // Assert
        attachment.ContentType.ShouldBe(WorkshopAttachment.DefaultContentType);
    }

    [Test]
    [Arguments("image/png", true)]
    [Arguments("image/jpeg", true)]
    [Arguments("image/gif", true)]
    [Arguments("image/webp", true)]
    [Arguments("IMAGE/PNG", true)]
    [Arguments("image/svg+xml", false)]
    [Arguments("text/html", false)]
    [Arguments("application/xhtml+xml", false)]
    [Arguments("application/pdf", false)]
    [Arguments("application/octet-stream", false)]
    public void only_a_png_jpeg_gif_or_webp_image_gets_a_preview(string contentType, bool hasPreview)
    {
        // Arrange
        TestGraph.Start(out _, Clock.Fixed).AddVenue(out _).AddWorkshop(out _)
            .AddWorkshopAttachment(out var attachment, a => a.ContentType = contentType);

        // Act
        var preview = attachment.PreviewAt(Clock.Now);

        // Assert
        attachment.HasPreview.ShouldBe(hasPreview);
        (preview is not null).ShouldBe(hasPreview);
    }

    [Test]
    public void the_download_is_an_attachment_with_the_encoded_name_and_the_stored_type_for_15_minutes()
    {
        // Arrange
        TestGraph.Start(out _, Clock.Fixed).AddVenue(out _).AddWorkshop(out _)
            .AddWorkshopAttachment(out var attachment, a =>
            {
                a.FileName = "résumé \"final\"; v2.png";
                a.ContentType = "image/png";
            });

        // Act
        var download = attachment.DownloadAt(Clock.Now);

        // Assert
        download.Key.ShouldBe(attachment.StorageKey);
        download.ContentType.ShouldBe("image/png");
        download.ContentDisposition.ShouldBe("attachment; filename*=UTF-8''r%C3%A9sum%C3%A9%20%22final%22%3B%20v2.png");
        download.ExpiresAt.ShouldBe(Clock.Now + TimeSpan.FromMinutes(15));
    }

    [Test]
    public void the_preview_is_inline_with_the_stored_type_and_the_same_expiry()
    {
        // Arrange
        TestGraph.Start(out _, Clock.Fixed).AddVenue(out _).AddWorkshop(out _)
            .AddWorkshopAttachment(out var attachment, a =>
            {
                a.FileName = "kiln.webp";
                a.ContentType = "image/webp";
            });

        // Act
        var preview = attachment.PreviewAt(Clock.Now).ShouldNotBeNull();

        // Assert
        preview.ContentType.ShouldBe("image/webp");
        preview.ContentDisposition.ShouldBe("inline; filename*=UTF-8''kiln.webp");
        preview.ExpiresAt.ShouldBe(attachment.DownloadAt(Clock.Now).ExpiresAt);
    }
}
