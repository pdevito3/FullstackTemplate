namespace FullstackTemplate.IntegrationTests.ObjectDeletes;

using System.Text;
using BackWave.Storage;
using FullstackTemplate.IntegrationTests.Jobs;
using FullstackTemplate.Server.Domain.ObjectDeletes;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;
using static ObjectDeleteTestServer;

/// <summary>
/// Issue 422: an object delete that fails after the row delete commits. The save commits the job with the delete. The
/// test reads the job from the database and runs it through BackWave on the real store, with storage up, down, or hung.
/// </summary>
public class ObjectDeleteJobTests : TestBase
{
    private static readonly ObjectDeleteOptions ThreeAttempts = new()
    {
        MaxAttempts = 3,
        FirstRetryDelay = TimeSpan.FromSeconds(30),
        MaxRetryDelay = TimeSpan.FromSeconds(45),
    };

    [Test]
    public async Task a_tenant_delete_commits_one_job_that_deletes_each_object_of_the_tenant()
    {
        // Arrange
        var (tenantId, admin) = await AddTenantAsync();
        var prefix = $"tenants/{tenantId}/";
        await PutAsync($"{prefix}files/{Guid.NewGuid():N}");
        // An object that no row names, as a failed add can leave
        await PutAsync($"{prefix}no-row/{Guid.NewGuid():N}");
        await using var storageUp = WithStorageUp();
        await using var harness = Harness(storageUp);

        // Act
        await DeleteTenantAsync(tenantId, admin);
        var queued = await StoredJobs.QueuedAsync<DeleteTenantObjects>(tenantId);
        var keysAfterSave = await KeysUnderAsync(prefix);
        var jobId = await harness.EnqueueAsync(queued.Single());
        await harness.RunDueAsync();

        // Assert
        queued.ShouldBe([new DeleteTenantObjects(tenantId)]);
        (await StoredJobs.QueuedAsync<DeleteObject>(tenantId)).ShouldBeEmpty("The job of the tenant covers its attachments.");
        keysAfterSave.Count.ShouldBe(2, "The save does not call storage. The job does.");
        (await harness.JobAsync(jobId)).State.ShouldBe(JobState.Succeeded);
        (await KeysUnderAsync(prefix)).ShouldBeEmpty();
    }

    [Test]
    public async Task a_job_with_storage_down_runs_again_after_the_first_delay_and_deletes_the_objects_when_storage_is_back()
    {
        // Arrange
        var (tenantId, key) = await DeleteTenantWithObjectAsync();
        await using var storageDown = WithStorageDown();
        await using var storageUp = WithStorageUp();
        await using var harness = Harness(storageDown);
        var firstRetryDelay = new ObjectDeleteOptions().FirstRetryDelay;

        // Act
        var jobId = await harness.EnqueueAsync(new DeleteTenantObjects(tenantId));
        await harness.RunDueAsync();
        var afterFailure = await harness.JobAsync(jobId);
        var keysWhileDown = await KeysUnderAsync(key);
        harness.UseServer(storageUp);
        await harness.AdvanceAsync(firstRetryDelay - TimeSpan.FromSeconds(1));
        var beforeDelay = await harness.JobAsync(jobId);
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));

        // Assert
        afterFailure.ShouldSatisfyAllConditions(
            j => j.State.ShouldBe(JobState.Scheduled),
            j => j.Attempt.ShouldBe(1));
        keysWhileDown.ShouldBe([key], "Storage was down, so the object stayed.");
        beforeDelay.Attempt.ShouldBe(1, "A job does not run again before its delay ends.");
        (await harness.JobAsync(jobId)).State.ShouldBe(JobState.Succeeded);
        (await KeysUnderAsync(key)).ShouldBeEmpty("With storage back, the next attempt deleted the object.");
    }

    [Test]
    public async Task a_job_that_fails_each_attempt_waits_a_delay_that_doubles_up_to_the_largest_and_is_dead_after_the_last()
    {
        // Arrange
        var (tenantId, key) = await DeleteTenantWithObjectAsync();
        await using var storageDown = WithStorageDown(ThreeAttempts);
        await using var harness = Harness(storageDown, ThreeAttempts);
        var start = harness.Now;

        // Act
        var jobId = await harness.EnqueueAsync(new DeleteTenantObjects(tenantId));
        await harness.RunDueAsync();
        var afterFirst = await harness.JobAsync(jobId);
        await harness.AdvanceAsync(TimeSpan.FromSeconds(30));
        var afterSecond = await harness.JobAsync(jobId);
        await harness.AdvanceAsync(TimeSpan.FromSeconds(45));
        var afterThird = await harness.JobAsync(jobId);
        await harness.AdvanceAsync(TimeSpan.FromDays(1));

        // Assert
        afterFirst.DueTime.ShouldBe(start + TimeSpan.FromSeconds(30));
        afterSecond.ShouldSatisfyAllConditions(
            j => j.Attempt.ShouldBe(2),
            j => j.DueTime.ShouldBe(start + TimeSpan.FromSeconds(30) + TimeSpan.FromSeconds(45),
                "The second delay of 60 seconds is cut to the largest delay of 45 seconds."));
        afterThird.ShouldSatisfyAllConditions(
            j => j.State.ShouldBe(JobState.DeadLettered),
            j => j.Attempt.ShouldBe(3));
        (await harness.JobAsync(jobId)).Attempt.ShouldBe(3, "No worker runs a dead job.");
        (await harness.FailuresOfAsync(jobId)).Count.ShouldBe(3);
        (await KeysUnderAsync(key)).ShouldBe([key], "The object of a dead job stays for a person.");
    }

    [Test]
    public async Task a_job_whose_objects_are_gone_succeeds()
    {
        // Arrange
        // The tenant has no object left, as after an attempt that deleted them and then stopped
        var (tenantId, admin) = await AddTenantAsync();
        await DeleteTenantAsync(tenantId, admin);
        await using var storageUp = WithStorageUp();
        await using var harness = Harness(storageUp);

        // Act
        var jobId = await harness.EnqueueAsync((await StoredJobs.QueuedAsync<DeleteTenantObjects>(tenantId)).Single());
        await harness.RunDueAsync();

        // Assert
        (await harness.JobAsync(jobId)).State.ShouldBe(JobState.Succeeded);
    }

    [Test]
    public async Task an_attempt_on_a_storage_that_does_not_answer_fails_at_the_attempt_timeout()
    {
        // Arrange
        var (tenantId, key) = await DeleteTenantWithObjectAsync();
        var options = ThreeAttempts with { AttemptTimeout = TimeSpan.FromSeconds(1) };
        await using var hung = WithStorageHung(options);
        await using var harness = Harness(hung.Server, options);

        // Act
        var jobId = await harness.EnqueueAsync(new DeleteTenantObjects(tenantId));
        await harness.RunDueAsync().WaitAsync(TimeSpan.FromSeconds(30));

        // Assert
        (await harness.JobAsync(jobId)).ShouldSatisfyAllConditions(
            j => j.State.ShouldBe(JobState.Scheduled),
            j => j.Attempt.ShouldBe(1));
        (await harness.FailuresOfAsync(jobId)).ShouldHaveSingleItem()
            .ShouldContain("System.TimeoutException: The object delete took more than 00:00:01.");
        (await KeysUnderAsync(key)).ShouldBe([key]);
    }

    [Test]
    public async Task the_job_of_a_tenant_keeps_the_objects_of_another_tenant()
    {
        // Arrange
        var (deleted, admin) = await AddTenantAsync();
        await new TestingServiceScope().StartGraph(out _).AddTenant(out var kept).BuildAsync();
        await PutAsync($"tenants/{deleted}/files/{Guid.NewGuid():N}");
        await PutAsync($"tenants/{kept.Id}/files/{Guid.NewGuid():N}");
        await PutAsync($"tenants/{kept.Id}/no-row/{Guid.NewGuid():N}");
        await DeleteTenantAsync(deleted, admin);
        await using var storageUp = WithStorageUp();
        await using var harness = Harness(storageUp);

        // Act
        await harness.EnqueueAsync((await StoredJobs.QueuedAsync<DeleteTenantObjects>(deleted)).Single());
        await harness.RunDueAsync();

        // Assert
        (await KeysUnderAsync($"tenants/{deleted}/")).ShouldBeEmpty();
        (await KeysUnderAsync($"tenants/{kept.Id}/")).Count.ShouldBe(2);
    }

    [Test]
    public async Task a_stored_object_delete_with_a_key_of_another_tenant_is_quarantined_and_deletes_nothing()
    {
        // Arrange
        var (tenantId, _) = await AddTenantAsync();
        var (otherTenantId, _) = await AddTenantAsync();
        var ownKey = $"tenants/{tenantId}/files/{Guid.NewGuid():N}";
        var otherKey = $"tenants/{otherTenantId}/files/{Guid.NewGuid():N}";
        await PutAsync(otherKey);
        await using var storageUp = WithStorageUp();
        await using var harness = Harness(storageUp);
        // A payload that no save writes, as a hand edit of the store or a bug can leave
        var payload = Encoding.UTF8.GetString(StoredJobs.RegistrationOf<DeleteObject>(storageUp)
            .Serialize(new DeleteObject(tenantId, ownKey))).Replace(ownKey, otherKey, StringComparison.Ordinal);
        var jobId = Guid.NewGuid();
        await harness.Store.EnqueueAsync(new NewJob(jobId, "delete-object", Encoding.UTF8.GetBytes(payload),
            ObjectDeleteOptions.Queue, harness.Now), harness.Now);

        // Act
        await harness.RunDueAsync();

        // Assert
        (await harness.JobAsync(jobId)).State.ShouldBe(JobState.Quarantined);
        (await KeysUnderAsync(otherKey)).ShouldBe([otherKey]);
    }

    /// <summary>Deletes a new tenant that has one object in storage, and gives the tenant and the key of the object.</summary>
    private static async Task<(string TenantId, string Key)> DeleteTenantWithObjectAsync()
    {
        var (tenantId, admin) = await AddTenantAsync();
        var key = $"tenants/{tenantId}/files/{Guid.NewGuid():N}";
        await PutAsync(key);
        await DeleteTenantAsync(tenantId, admin);
        return (tenantId, key);
    }
}
