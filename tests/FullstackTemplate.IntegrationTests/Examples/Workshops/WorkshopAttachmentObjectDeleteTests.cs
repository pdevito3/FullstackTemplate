namespace FullstackTemplate.IntegrationTests.Examples.Workshops;

using BackWave.Storage;
using FullstackTemplate.IntegrationTests.Jobs;
using FullstackTemplate.Server.Domain.ObjectDeletes;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.Server.Examples.Workshops.Features;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using static FullstackTemplate.IntegrationTests.ObjectDeletes.ObjectDeleteTestServer;

/// <summary>
/// Issue 422 for the attachment delete: an object delete that fails after the row delete commits. The objects are on
/// the real store. A Server with storage down has an S3 client whose endpoint refuses each connection.
/// </summary>
public class WorkshopAttachmentObjectDeleteTests : TestBase
{
    [Test]
    public async Task an_attachment_delete_with_storage_down_deletes_the_object_after_storage_is_back()
    {
        // Arrange
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop).AddWorkshopAttachment(out var attachment)
            .BuildAsync();
        var key = attachment.File.StorageKey;
        await PutAsync(key);
        await using var storageDown = WithStorageDown();
        await using var storageUp = WithStorageUp();
        await using var harness = Harness(storageDown);
        var underTest = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        await underTest.SendAsync(new DeleteWorkshopAttachment.Command(workshop.Id, attachment.Id));
        var queued = await StoredJobs.QueuedAsync<DeleteObject>(tenant.Id);
        var jobId = await harness.EnqueueAsync(queued.Single());
        await harness.RunDueAsync();
        var keysWhileDown = await KeysUnderAsync(key);
        harness.UseServer(storageUp);
        await harness.AdvanceAsync(new ObjectDeleteOptions().FirstRetryDelay);

        // Assert
        var row = await new TestingServiceScope().ExecuteDbContextAsync(db => db.Set<WorkshopAttachment>()
            .IgnoreQueryFilters().SingleAsync(a => a.Id == attachment.Id));
        row.IsDeleted.ShouldBeTrue("The row delete committed.");
        queued.ShouldBe([new DeleteObject(tenant.Id, key)]);
        keysWhileDown.ShouldBe([key], "Storage was down, so the object stayed.");
        (await harness.JobAsync(jobId)).ShouldSatisfyAllConditions(
            j => j.State.ShouldBe(JobState.Succeeded),
            j => j.Attempt.ShouldBe(2));
        (await KeysUnderAsync(key)).ShouldBeEmpty("With storage back, the next attempt deleted the object.");
    }

    [Test]
    public async Task a_tenant_delete_covers_the_objects_of_its_attachments_with_its_one_job()
    {
        // Arrange
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out _).AddWorkshopAttachment(out var attachment)
            .BuildAsync();
        var prefix = $"tenants/{tenant.Id}/";
        await PutAsync(attachment.File.StorageKey);
        await using var storageUp = WithStorageUp();
        await using var harness = Harness(storageUp);

        // Act
        await DeleteTenantAsync(tenant.Id, admin.Identifier);
        var tenantJobs = await StoredJobs.QueuedAsync<DeleteTenantObjects>(tenant.Id);
        var objectJobs = await StoredJobs.QueuedAsync<DeleteObject>(tenant.Id);
        await harness.EnqueueAsync(tenantJobs.Single());
        await harness.RunDueAsync();

        // Assert
        tenantJobs.ShouldBe([new DeleteTenantObjects(tenant.Id)]);
        objectJobs.ShouldBeEmpty("One job covers each object of the tenant, also the attachment.");
        (await KeysUnderAsync(prefix)).ShouldBeEmpty();
    }

    [Test]
    public async Task the_job_deletes_only_the_object_of_the_deleted_attachment()
    {
        // Arrange
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop)
            .AddWorkshopAttachments(out var attachments, 2)
            .BuildAsync();
        var (first, second) = (attachments[0], attachments[1]);
        await PutAsync(first.File.StorageKey);
        await PutAsync(second.File.StorageKey);
        await using var storageUp = WithStorageUp();
        await using var harness = Harness(storageUp);
        var underTest = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        await underTest.SendAsync(new DeleteWorkshopAttachment.Command(workshop.Id, first.Id));
        var keysAfterSave = await KeysUnderAsync(first.File.StorageKey);
        await harness.EnqueueAsync((await StoredJobs.QueuedAsync<DeleteObject>(tenant.Id)).Single());
        await harness.RunDueAsync();

        // Assert
        keysAfterSave.ShouldBe([first.File.StorageKey], "The delete does not call storage. The job does.");
        (await KeysUnderAsync($"tenants/{tenant.Id}/")).ShouldBe([second.File.StorageKey]);
    }

    [Test]
    public async Task the_job_of_an_attachment_whose_object_is_gone_succeeds()
    {
        // Arrange
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop).AddWorkshopAttachment(out var attachment)
            .BuildAsync();
        await using var storageUp = WithStorageUp();
        await using var harness = Harness(storageUp);
        var underTest = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        // The object of this attachment was never put, as after an attempt that deleted it and then stopped
        await underTest.SendAsync(new DeleteWorkshopAttachment.Command(workshop.Id, attachment.Id));
        var jobId = await harness.EnqueueAsync((await StoredJobs.QueuedAsync<DeleteObject>(tenant.Id)).Single());
        await harness.RunDueAsync();

        // Assert
        (await harness.JobAsync(jobId)).State.ShouldBe(JobState.Succeeded);
    }
}
