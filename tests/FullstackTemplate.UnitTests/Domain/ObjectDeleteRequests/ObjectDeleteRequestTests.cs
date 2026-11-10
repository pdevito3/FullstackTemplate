namespace FullstackTemplate.UnitTests.Domain.ObjectDeleteRequests;

using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.Server.Domain.ObjectDeleteRequests;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Attachments;
using Shouldly;

public class ObjectDeleteRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly UploadRules Pdf = new(8, FileType.Pdf);

    [Test]
    public void each_deleted_attachment_gets_a_request_for_its_object_that_is_due_at_once()
    {
        // Arrange
        Attachment[] attachments = [AttachmentOf("ten_a", "f1"), AttachmentOf("ten_a", "f2")];

        // Act
        var requests = ObjectDeleteRequest.ForDeleted([.. attachments, new object()], Now);

        // Assert
        requests.Select(r => r.Key).ShouldBe(attachments.Select(a => a.File.StorageKey), ignoreOrder: true);
        requests.ShouldAllBe(r => r.TenantId == "ten_a" && !r.IsPrefix && r.DueAt == Now && r.Attempts == 0);
    }

    [Test]
    public void a_deleted_tenant_gets_one_request_for_all_its_objects_and_its_attachments_get_none()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant);
        var other = AttachmentOf("ten_other", "f3");

        // Act
        var requests = ObjectDeleteRequest.ForDeleted(
            [tenant, AttachmentOf(tenant.Id, "f1"), AttachmentOf(tenant.Id, "f2"), other], Now);

        // Assert
        requests.Count.ShouldBe(2);
        requests.ShouldContain(r => r.TenantId == tenant.Id && r.Key == $"tenants/{tenant.Id}/" && r.IsPrefix);
        requests.ShouldContain(r => r.TenantId == "ten_other" && r.Key == other.File.StorageKey && !r.IsPrefix);
    }

    [Test]
    public void an_attachment_whose_key_is_not_under_its_tenant_stops_the_save()
    {
        // Arrange
        var attachment = AttachmentOf("ten_a", "f1");
        var wrong = attachment with { TenantId = "ten_other" };

        // Act
        var act = () => ObjectDeleteRequest.ForDeleted([wrong], Now);

        // Assert
        act.ShouldThrow<InvalidOperationException>();
    }

    [Test]
    public void a_long_error_is_cut_to_the_length_of_its_column()
    {
        // Arrange
        var request = ObjectDeleteRequest.ForDeleted([AttachmentOf("ten_a", "f1")], Now).Single();

        // Act
        var failure = request.AfterFailure(new string('x', ObjectDeleteRequest.LastErrorMaxLength + 1), Now, new ObjectDeleteOptions());

        // Assert
        failure.LastError.Length.ShouldBe(ObjectDeleteRequest.LastErrorMaxLength);
        failure.IsDead.ShouldBeFalse();
    }

    private static Attachment AttachmentOf(string tenantId, string fileId)
        => new(fileId, tenantId,
            StoredFile.Accept(SampleFiles.Upload("notes.pdf", SampleFiles.Pdf), Pdf, tenantId, $"things/t1/attachments/{fileId}"), Now);

    private sealed record Attachment(string Id, string TenantId, StoredFile File, DateTimeOffset UploadedAt) : IAttachment;
}
