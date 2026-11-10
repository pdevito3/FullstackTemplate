namespace FullstackTemplate.IntegrationTests.Resources.Storage;

using FullstackTemplate.Server.Domain.ObjectDeleteRequests;
using FullstackTemplate.Server.Resources.Storage;
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
    public async Task a_tenant_delete_with_storage_down_deletes_its_objects_after_storage_is_back()
    {
        // Arrange
        var (tenantId, admin) = await AddTenantAsync();
        var prefix = $"tenants/{tenantId}/";
        await PutAsync($"{prefix}files/{Guid.NewGuid():N}");
        // An object that no row names, as a failed add can leave
        await PutAsync($"{prefix}no-row/{Guid.NewGuid():N}");
        await using var storageDown = WithStorageDown();
        await using var storageUp = WithStorageUp();

        // Act
        await DeleteTenantAsync(tenantId, admin, storageDown);
        var requests = await RequestsOfTenantAsync(tenantId);
        var now = DateTimeOffset.UtcNow;
        var failed = await RunDueAsync(storageDown, now);
        var keysWhileDown = await KeysUnderAsync(prefix);
        var deleted = await RunDueAsync(storageUp, now + FirstRetryDelay);

        // Assert
        var request = requests.ShouldHaveSingleItem();
        request.ShouldSatisfyAllConditions(
            r => r.Key.ShouldBe(prefix),
            r => r.IsPrefix.ShouldBeTrue("One request covers each object of the tenant."));
        failed.ShouldContain(new ObjectDeleteAttempt(request.Id, ObjectDeleteOutcome.RetryLater));
        keysWhileDown.Count.ShouldBe(2, "Storage was down, so the objects stayed.");
        deleted.ShouldContain(new ObjectDeleteAttempt(request.Id, ObjectDeleteOutcome.Deleted));
        (await KeysUnderAsync(prefix)).ShouldBeEmpty("With storage back, the next attempt deleted each object.");
        (await RequestsOfTenantAsync(tenantId)).ShouldBeEmpty("A done request leaves the queue.");
    }

    [Test]
    public async Task with_storage_up_the_objects_of_a_deleted_tenant_stay_until_the_next_run()
    {
        // Arrange
        var (tenantId, admin) = await AddTenantAsync();
        var key = $"tenants/{tenantId}/files/{Guid.NewGuid():N}";
        await PutAsync(key);
        await using var storageUp = WithStorageUp();

        // Act and assert
        await DeleteTenantAsync(tenantId, admin, storageUp);
        (await KeysUnderAsync(key)).ShouldBe([key], "The delete does not call storage. The next run does.");
        await RunDueAsync(storageUp, DateTimeOffset.UtcNow);
        (await KeysUnderAsync(key)).ShouldBeEmpty();
    }

    [Test]
    public async Task a_repeat_delete_of_objects_that_are_gone_is_harmless_on_the_real_store()
    {
        // Arrange
        var (tenantId, admin) = await AddTenantAsync();
        var key = $"tenants/{Guid.NewGuid():N}/files/one";
        var prefix = $"tenants/{Guid.NewGuid():N}/";
        await PutAsync(key);
        await PutAsync($"{prefix}files/two");
        var storage = new TestingServiceScope().GetService<ObjectStorage>();
        await using var storageUp = WithStorageUp();

        // Act
        await storage.DeleteAsync(key, CancellationToken.None);
        await storage.DeleteAsync(key, CancellationToken.None);
        var first = await storage.DeleteUnderAsync(prefix, CancellationToken.None);
        var second = await storage.DeleteUnderAsync(prefix, CancellationToken.None);
        // The tenant has no object left, as after an attempt that deleted them and then stopped
        await DeleteTenantAsync(tenantId, admin, storageUp);
        var requestId = (await RequestsOfTenantAsync(tenantId)).Single().Id;
        var attempts = await RunDueAsync(storageUp, DateTimeOffset.UtcNow);

        // Assert
        first.ShouldBe(1);
        second.ShouldBe(0);
        (await KeysUnderAsync(key)).ShouldBeEmpty();
        (await KeysUnderAsync(prefix)).ShouldBeEmpty();
        attempts.ShouldContain(new ObjectDeleteAttempt(requestId, ObjectDeleteOutcome.Deleted));
        (await RequestsOfTenantAsync(tenantId)).ShouldBeEmpty("The delete of objects that are gone succeeds.");
    }
}
