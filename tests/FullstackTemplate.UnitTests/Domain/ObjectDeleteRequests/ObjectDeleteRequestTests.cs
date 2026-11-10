namespace FullstackTemplate.UnitTests.Domain.ObjectDeleteRequests;

using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.Server.Domain.ObjectDeleteRequests;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using Shouldly;

public class ObjectDeleteRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void each_deleted_attachment_gets_a_request_for_its_object_that_is_due_at_once()
    {
        // Arrange
        TestGraph.Start(out var seed).AddVenue(out _).AddWorkshop(out _).AddWorkshopAttachments(out var attachments, 2);

        // Act
        var requests = ObjectDeleteRequest.ForDeleted([.. attachments, new object()], Now);

        // Assert
        requests.Select(r => r.Key).ShouldBe(attachments.Select(a => a.File.StorageKey), ignoreOrder: true);
        requests.ShouldAllBe(r => r.TenantId == seed.Tenant.Id && !r.IsPrefix && r.DueAt == Now && r.Attempts == 0);
    }

    [Test]
    public void a_deleted_tenant_gets_one_request_for_all_its_objects_and_its_attachments_get_none()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant)
            .Under(tenant).AddVenue(out _).AddWorkshop(out _).AddWorkshopAttachments(out var attachments, 2);

        // Act
        var requests = ObjectDeleteRequest.ForDeleted([tenant, .. attachments], Now);

        // Assert
        requests.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            r => r.TenantId.ShouldBe(tenant.Id),
            r => r.Key.ShouldBe($"tenants/{tenant.Id}/"),
            r => r.IsPrefix.ShouldBeTrue());
    }

    [Test]
    public void an_attachment_whose_key_is_not_under_its_tenant_stops_the_save()
    {
        // Arrange
        TestGraph.Start(out _).AddVenue(out _).AddWorkshop(out _).AddWorkshopAttachment(out var attachment);
        var wrong = new Attachment(attachment.Id, "ten_other", attachment.File, attachment.UploadedAt);

        // Act
        var act = () => ObjectDeleteRequest.ForDeleted([wrong], Now);

        // Assert
        act.ShouldThrow<InvalidOperationException>();
    }

    [Test]
    public void a_long_error_is_cut_to_the_length_of_its_column()
    {
        // Arrange
        TestGraph.Start(out _).AddVenue(out _).AddWorkshop(out _).AddWorkshopAttachment(out var attachment);
        var request = ObjectDeleteRequest.ForDeleted([attachment], Now).Single();

        // Act
        var failure = request.AfterFailure(new string('x', ObjectDeleteRequest.LastErrorMaxLength + 1), Now, new ObjectDeleteOptions());

        // Assert
        failure.LastError.Length.ShouldBe(ObjectDeleteRequest.LastErrorMaxLength);
        failure.IsDead.ShouldBeFalse();
    }

    private sealed record Attachment(string Id, string TenantId, StoredFile File, DateTimeOffset UploadedAt) : IAttachment;
}
