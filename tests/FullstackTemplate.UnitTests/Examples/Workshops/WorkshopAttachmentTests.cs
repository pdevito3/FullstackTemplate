namespace FullstackTemplate.UnitTests.Examples.Workshops;

using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.Server.Examples.Workshops.DomainEvents;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Attachments;
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
        attachment.File.StorageKey.ShouldBe($"tenants/{seed.Tenant.Id}/workshops/{workshop.Id}/attachments/{attachment.Id}");
        attachment.DomainEvents.ShouldBe([new WorkshopAttachmentAdded(attachment.Id, workshop.Id)]);
    }

    [Test]
    public void a_workshop_takes_images_that_show_in_the_page_and_pdf_documents_up_to_10_megabytes()
    {
        // Act
        var rules = WorkshopAttachment.UploadRules;

        // Assert
        rules.FileTypes.ShouldBe([FileType.Png, FileType.Jpeg, FileType.Gif, FileType.WebP, FileType.Pdf]);
        rules.MaxSizeMegabytes.ShouldBe(10);
    }

    [Test]
    public void a_file_of_exactly_the_largest_size_is_taken()
    {
        // Act
        TestGraph.Start(out _, Clock.Fixed).AddVenue(out _).AddWorkshop(out _)
            .AddWorkshopAttachment(out var attachment, a => a.SizeBytes = WorkshopAttachment.UploadRules.MaxSizeBytes);

        // Assert
        attachment.File.SizeBytes.ShouldBe(WorkshopAttachment.UploadRules.MaxSizeBytes);
    }

    public static IEnumerable<Func<(Action<WorkshopAttachmentInput> Customize, string Field, string Message)>> RefusedFiles()
    {
        yield return () => (a => a.SizeBytes = WorkshopAttachment.UploadRules.MaxSizeBytes + 1, "sizeBytes",
            WorkshopAttachment.UploadRules.FileTooLargeMessage);
        yield return () => (a =>
        {
            a.FileName = "notes.txt";
            a.ClaimedMediaType = "text/plain";
            a.Head = SampleFiles.Text;
        }, "mediaType", WorkshopAttachment.UploadRules.TypeNotAllowedMessage);
        yield return () => (a => a.Head = SampleFiles.Png, "fileName", FileType.Png.ExtensionMismatchMessage);
        yield return () => (a => a.FileName = "../notes.pdf", "fileName",
            "A file name cannot have a slash or a backslash. Rename the file, then upload it again.");
    }

    [Test]
    [MethodDataSource(nameof(RefusedFiles))]
    public void an_attachment_refuses_a_file_that_the_rules_of_a_workshop_do_not_take(
        Action<WorkshopAttachmentInput> customize, string field, string message)
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
    public void an_image_of_a_workshop_gets_a_preview_for_the_life_of_a_url()
    {
        // Arrange
        TestGraph.Start(out _, Clock.Fixed).AddVenue(out _).AddWorkshop(out _)
            .AddWorkshopAttachment(out var attachment, a =>
            {
                a.FileName = "kiln.webp";
                a.ClaimedMediaType = "image/webp";
                a.Head = SampleFiles.WebP;
            });

        // Act
        var preview = attachment.File.PreviewAt(Clock.Now).ShouldNotBeNull();

        // Assert
        preview.Key.ShouldBe(attachment.File.StorageKey);
        preview.ContentDisposition.ShouldBe("inline; filename*=UTF-8''kiln.webp");
        preview.ExpiresAt.ShouldBe(Clock.Now + StoredFile.UrlLifetime);
    }
}
