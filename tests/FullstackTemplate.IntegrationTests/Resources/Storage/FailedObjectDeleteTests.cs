namespace FullstackTemplate.IntegrationTests.Resources.Storage;

using FullstackTemplate.Server.Domain.ObjectDeleteRequests;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.Server.Examples.Workshops.Features;
using FullstackTemplate.Server.Resources.Storage;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using static ObjectDeleteTestServer;

/// <summary>
/// Issue 422: an object delete that fails after the row delete commits. The objects are on the real store. A Server
/// with storage down has an S3 client whose endpoint refuses each connection, as a stopped storage does.
/// </summary>
[NotInParallel(RunnerTests)]
public class FailedObjectDeleteTests : TestBase
{
    private static readonly TimeSpan FirstRetryDelay = new ObjectDeleteOptions().FirstRetryDelay;

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
        var underTest = new TestingServiceScope(storageDown);
        underTest.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        await underTest.SendAsync(new DeleteWorkshopAttachment.Command(workshop.Id, attachment.Id));
        var requestId = (await RequestsOfTenantAsync(tenant.Id)).Single().Id;
        var now = DateTimeOffset.UtcNow;
        var failed = await RunDueAsync(storageDown, now);
        var keysWhileDown = await KeysUnderAsync(key);
        var deleted = await RunDueAsync(storageUp, now + FirstRetryDelay);

        // Assert
        var row = await new TestingServiceScope().ExecuteDbContextAsync(db => db.Set<WorkshopAttachment>()
            .IgnoreQueryFilters().SingleAsync(a => a.Id == attachment.Id));
        row.IsDeleted.ShouldBeTrue("The row delete committed.");
        keysWhileDown.ShouldBe([key], "Storage was down, so the object stayed.");
        failed.ShouldContain(new ObjectDeleteAttempt(requestId, ObjectDeleteOutcome.RetryLater));
        deleted.ShouldContain(new ObjectDeleteAttempt(requestId, ObjectDeleteOutcome.Deleted));
        (await KeysUnderAsync(key)).ShouldBeEmpty("With storage back, the next attempt deleted the object.");
        (await RequestsOfTenantAsync(tenant.Id)).ShouldBeEmpty("A done request leaves the queue.");
    }

    [Test]
    public async Task a_tenant_delete_with_storage_down_deletes_its_objects_after_storage_is_back()
    {
        // Arrange
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out _).AddWorkshopAttachment(out var attachment)
            .BuildAsync();
        var prefix = $"tenants/{tenant.Id}/";
        await PutAsync(attachment.File.StorageKey);
        // An object that no row names, as a failed add can leave
        await PutAsync($"{prefix}no-row/{Guid.NewGuid():N}");
        await using var storageDown = WithStorageDown();
        await using var storageUp = WithStorageUp();
        var underTest = new TestingServiceScope(storageDown);
        underTest.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        await underTest.SendAsync(new DeleteTenant.Command(tenant.Id));
        var requests = await RequestsOfTenantAsync(tenant.Id);
        var now = DateTimeOffset.UtcNow;
        await RunDueAsync(storageDown, now);
        var keysWhileDown = await KeysUnderAsync(prefix);
        await RunDueAsync(storageUp, now + FirstRetryDelay);

        // Assert
        requests.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            r => r.Key.ShouldBe(prefix),
            r => r.IsPrefix.ShouldBeTrue("One request covers each object of the tenant, also the attachment."));
        keysWhileDown.Count.ShouldBe(2, "Storage was down, so the objects stayed.");
        (await KeysUnderAsync(prefix)).ShouldBeEmpty("With storage back, the next attempt deleted each object.");
        (await RequestsOfTenantAsync(tenant.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task with_storage_up_the_object_of_a_deleted_row_stays_until_the_next_run()
    {
        // Arrange
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop)
            .AddWorkshopAttachments(out var attachments, 2)
            .BuildAsync();
        var (first, second) = (attachments[0], attachments[1]);
        var firstKey = first.File.StorageKey;
        var prefix = $"tenants/{tenant.Id}/";
        await PutAsync(firstKey);
        await PutAsync(second.File.StorageKey);
        await using var storageUp = WithStorageUp();
        var underTest = new TestingServiceScope(storageUp);
        underTest.SetUserWithDefaults(identifier: admin.Identifier);

        // Act and assert
        await underTest.SendAsync(new DeleteWorkshopAttachment.Command(workshop.Id, first.Id));
        (await KeysUnderAsync(firstKey)).ShouldBe([firstKey], "The delete does not call storage. The next run does.");
        await RunDueAsync(storageUp, DateTimeOffset.UtcNow);
        (await KeysUnderAsync(prefix)).ShouldBe([second.File.StorageKey]);

        await underTest.SendAsync(new DeleteTenant.Command(tenant.Id));
        await RunDueAsync(storageUp, DateTimeOffset.UtcNow);
        (await KeysUnderAsync(prefix)).ShouldBeEmpty();
    }

    [Test]
    public async Task a_repeat_delete_of_objects_that_are_gone_is_harmless_on_the_real_store()
    {
        // Arrange
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop).AddWorkshopAttachment(out var attachment)
            .BuildAsync();
        var key = $"tenants/{Guid.NewGuid():N}/files/one";
        var prefix = $"tenants/{Guid.NewGuid():N}/";
        await PutAsync(key);
        await PutAsync($"{prefix}files/two");
        var storage = new TestingServiceScope().GetService<ObjectStorage>();
        await using var storageUp = WithStorageUp();
        var underTest = new TestingServiceScope(storageUp);
        underTest.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        await storage.DeleteAsync(key, CancellationToken.None);
        await storage.DeleteAsync(key, CancellationToken.None);
        var first = await storage.DeleteUnderAsync(prefix, CancellationToken.None);
        var second = await storage.DeleteUnderAsync(prefix, CancellationToken.None);
        // The object of this attachment was never put, as after an attempt that deleted it and then stopped
        await underTest.SendAsync(new DeleteWorkshopAttachment.Command(workshop.Id, attachment.Id));
        var attempts = await RunDueAsync(storageUp, DateTimeOffset.UtcNow);

        // Assert
        first.ShouldBe(1);
        second.ShouldBe(0);
        (await KeysUnderAsync(key)).ShouldBeEmpty();
        (await KeysUnderAsync(prefix)).ShouldBeEmpty();
        attempts.ShouldContain(a => a.Outcome == ObjectDeleteOutcome.Deleted);
        (await RequestsOfTenantAsync(tenant.Id)).ShouldBeEmpty("The delete of a gone object succeeds.");
    }
}
