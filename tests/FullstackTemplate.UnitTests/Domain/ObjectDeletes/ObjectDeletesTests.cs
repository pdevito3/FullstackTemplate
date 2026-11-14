namespace FullstackTemplate.UnitTests.Domain.ObjectDeletes;

using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.Server.Domain.ObjectDeletes;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Attachments;
using Shouldly;

public class ObjectDeletesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly UploadRules Pdf = new(8, FileType.Pdf);

    [Test]
    public void each_deleted_attachment_gets_a_job_for_its_object()
    {
        // Arrange
        Attachment[] attachments = [AttachmentOf("ten_a", "f1"), AttachmentOf("ten_a", "f2")];

        // Act
        var jobs = ObjectDeletes.ForDeleted([.. attachments, new object()]).Select(j => j.Job);

        // Assert
        jobs.ShouldBe(attachments.Select(a => new DeleteObject("ten_a", a.File.StorageKey)), ignoreOrder: true);
    }

    [Test]
    public void a_deleted_tenant_gets_one_job_for_all_its_objects_and_its_attachments_get_none()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant);
        var other = AttachmentOf("ten_other", "f3");

        // Act
        var jobs = ObjectDeletes.ForDeleted([tenant, AttachmentOf(tenant.Id, "f1"), AttachmentOf(tenant.Id, "f2"), other])
            .Select(j => j.Job);

        // Assert
        jobs.ShouldBe([new DeleteTenantObjects(tenant.Id), new DeleteObject("ten_other", other.File.StorageKey)],
            ignoreOrder: true);
    }

    [Test]
    public void an_attachment_whose_key_is_not_under_its_tenant_stops_the_save()
    {
        // Arrange
        var wrong = AttachmentOf("ten_a", "f1") with { TenantId = "ten_other" };

        // Act
        var act = () => ObjectDeletes.ForDeleted([wrong]);

        // Assert
        act.ShouldThrow<ArgumentException>().Message
            .ShouldStartWith("The key of an object delete of tenant ten_other must start with tenants/ten_other/.");
    }

    [Test]
    [Arguments("ten_a", "tenants/ten_b/files/f1")]
    [Arguments("ten_a", "tenants/ten_a")]
    [Arguments("ten_a/..", "tenants/ten_a/../ten_b/f1")]
    [Arguments("usr_a", "tenants/usr_a/files/f1")]
    [Arguments(null, "tenants//files/f1")]
    [Arguments("ten_a", null)]
    public void an_object_delete_needs_a_key_under_the_prefix_of_a_tenant(string? tenantId, string? key)
    {
        // Act
        var act = () => new DeleteObject(tenantId!, key!);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Test]
    [Arguments("ten_a/..")]
    [Arguments("ten_")]
    [Arguments("usr_a")]
    [Arguments("")]
    [Arguments(null)]
    public void a_tenant_delete_needs_a_tenant_id(string? tenantId)
    {
        // Act
        var act = () => new DeleteTenantObjects(tenantId!);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Test]
    public async Task an_attempt_that_takes_longer_than_the_timeout_ends_with_a_timeout()
    {
        // Act
        var act = () => ObjectDeletes.InAttemptTimeAsync(TimeSpan.FromMilliseconds(10), async attempt =>
        {
            await Task.Delay(Timeout.Infinite, attempt);
            return 1;
        }, CancellationToken.None);

        // Assert
        (await act.ShouldThrowAsync<TimeoutException>()).Message.ShouldBe("The object delete took more than 00:00:00.0100000.");
    }

    [Test]
    public async Task a_cancel_of_the_attempt_by_backwave_stays_a_cancel()
    {
        // Arrange
        using var shutdown = new CancellationTokenSource();

        // Act
        var attempt = ObjectDeletes.InAttemptTimeAsync(TimeSpan.FromMinutes(1), async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return 1;
        }, shutdown.Token);
        await shutdown.CancelAsync();

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(() => attempt);
    }

    private static Attachment AttachmentOf(string tenantId, string fileId)
        => new(fileId, tenantId,
            StoredFile.Accept(SampleFiles.Upload("notes.pdf", SampleFiles.Pdf), Pdf, tenantId, $"things/t1/attachments/{fileId}"), Now);

    private sealed record Attachment(string Id, string TenantId, StoredFile File, DateTimeOffset UploadedAt) : IAttachment;
}
