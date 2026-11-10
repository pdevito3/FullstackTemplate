namespace FullstackTemplate.IntegrationTests.Resources.Storage;

using System.Collections.Concurrent;
using System.Diagnostics;
using FullstackTemplate.Server.Domain.ObjectDeleteRequests;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;
using static ObjectDeleteTestServer;

[NotInParallel(RunnerTests)]
public class ObjectDeleteRunnerTests : TestBase
{
    private static readonly ObjectDeleteOptions ThreeAttempts = new()
    {
        MaxAttempts = 3,
        FirstRetryDelay = TimeSpan.FromSeconds(30),
        MaxRetryDelay = TimeSpan.FromSeconds(45),
    };

    [Test]
    public async Task a_failed_request_waits_a_delay_that_doubles_up_to_the_largest_delay()
    {
        // Arrange
        var (tenantId, _) = await DeleteTenantWithObjectAsync();
        await using var storageDown = WithStorageDown(ThreeAttempts);
        var start = DatabaseNow();

        // Act
        await RunDueAsync(storageDown, start);
        var afterFirst = (await RequestsOfTenantAsync(tenantId)).Single();
        var early = await RunDueAsync(storageDown, start + TimeSpan.FromSeconds(29));
        await RunDueAsync(storageDown, start + TimeSpan.FromSeconds(30));
        var afterSecond = (await RequestsOfTenantAsync(tenantId)).Single();

        // Assert
        afterFirst.ShouldSatisfyAllConditions(
            r => r.Attempts.ShouldBe(1),
            r => r.DueAt.ShouldBe(start + TimeSpan.FromSeconds(30)),
            r => r.DeadAt.ShouldBeNull(),
            r => r.LastError.ShouldNotBeNullOrEmpty());
        early.ShouldNotContain(a => a.RequestId == afterFirst.Id, "A request is not due before its delay ends.");
        afterSecond.Attempts.ShouldBe(2);
        afterSecond.DueAt.ShouldBe(start + TimeSpan.FromSeconds(30) + TimeSpan.FromSeconds(45),
            "The second delay of 60 seconds is cut to the largest delay of 45 seconds.");
    }

    [Test]
    public async Task the_delay_after_a_failure_starts_when_the_attempt_ends_not_when_the_run_starts()
    {
        // Arrange
        var (tenantId, _) = await DeleteTenantWithObjectAsync();
        var delay = TimeSpan.FromDays(1);
        // Each read of the clock is a minute later, so the attempt ends a minute or more after the run starts
        await using var storageDown = WithStorageDown(
            new ObjectDeleteOptions { FirstRetryDelay = delay, MaxRetryDelay = delay }, clockStep: TimeSpan.FromMinutes(1));
        var start = DatabaseNow();

        // Act
        await RunDueAsync(storageDown, start);

        // Assert
        (await RequestsOfTenantAsync(tenantId)).Single().DueAt.ShouldBeGreaterThanOrEqualTo(start + TimeSpan.FromMinutes(1) + delay);
    }

    [Test]
    public async Task a_request_that_fails_each_attempt_is_dead_and_its_span_shows_the_error()
    {
        // Arrange
        var (tenantId, key) = await DeleteTenantWithObjectAsync();
        await using var storageDown = WithStorageDown(ThreeAttempts);
        using var spans = new RunSpans();
        var start = DatabaseNow();
        var requestId = (await RequestsOfTenantAsync(tenantId)).Single().Id;

        // Act
        var first = await RunDueAsync(storageDown, start);
        var second = await RunDueAsync(storageDown, start + TimeSpan.FromSeconds(30));
        var third = await RunDueAsync(storageDown, start + TimeSpan.FromSeconds(75));
        var later = await RunDueAsync(storageDown, start + TimeSpan.FromDays(1));

        // Assert
        Of(requestId, first).ShouldBe([ObjectDeleteOutcome.RetryLater]);
        Of(requestId, second).ShouldBe([ObjectDeleteOutcome.RetryLater]);
        Of(requestId, third).ShouldBe([ObjectDeleteOutcome.Dead]);
        Of(requestId, later).ShouldBeEmpty("No runner claims a dead request.");
        (await RequestsOfTenantAsync(tenantId)).Single().ShouldSatisfyAllConditions(
            r => r.Attempts.ShouldBe(3),
            r => r.DeadAt.ShouldBe(start + TimeSpan.FromSeconds(75)),
            r => r.LastError.ShouldNotBeNullOrEmpty());
        (await KeysUnderAsync(key)).ShouldBe([key], "The object of a dead request stays for a person.");
        var runs = spans.Of(requestId);
        runs.Select(s => s.GetTagItem("app.object_delete.outcome")).ShouldBe(["retry_later", "retry_later", "dead"]);
        runs.Select(s => s.Status).ShouldBe([ActivityStatusCode.Unset, ActivityStatusCode.Unset, ActivityStatusCode.Error]);
        runs[0].Events.ShouldContain(e => e.Name == "object_delete.failed");
    }

    [Test]
    public async Task a_claim_after_a_runner_stopped_during_the_last_attempt_makes_the_request_dead()
    {
        // Arrange
        var (tenantId, _) = await DeleteTenantWithObjectAsync();
        var requestId = (await RequestsOfTenantAsync(tenantId)).Single().Id;
        // A runner claimed the last attempt and stopped before it wrote the outcome
        await TestFixture.ExecuteOutOfBandAsync("UPDATE object_delete_requests SET attempts = @attempts WHERE id = @id",
            ("attempts", ThreeAttempts.MaxAttempts), ("id", requestId));
        await using var storageUp = WithStorageUp(ThreeAttempts);

        // Act
        var attempts = await RunDueAsync(storageUp, DateTimeOffset.UtcNow);

        // Assert
        Of(requestId, attempts).ShouldBe([ObjectDeleteOutcome.Dead]);
        (await RequestsOfTenantAsync(tenantId)).Single().LastError.ShouldBe("The runner stopped during the last attempt.");
    }

    [Test]
    public async Task two_runners_at_the_same_time_claim_each_request_one_time()
    {
        // Arrange
        var tenants = new List<(string TenantId, string Key)>();
        for (var i = 0; i < 20; i++)
            tenants.Add(await DeleteTenantWithObjectAsync());
        var requestIds = new HashSet<string>();
        foreach (var (tenantId, _) in tenants)
            requestIds.Add((await RequestsOfTenantAsync(tenantId)).Single().Id);
        await using var first = WithStorageUp();
        await using var second = WithStorageUp();
        var now = DateTimeOffset.UtcNow;

        // Act
        var runs = await Task.WhenAll(RunDueAsync(first, now), RunDueAsync(second, now));

        // Assert
        var ours = runs.SelectMany(r => r).Where(a => requestIds.Contains(a.RequestId)).ToList();
        ours.Select(a => a.RequestId).Order().ShouldBe(requestIds.Order(), "Each request was claimed one time.");
        ours.ShouldAllBe(a => a.Outcome == ObjectDeleteOutcome.Deleted);
        runs.ShouldAllBe(r => r.Count > 0, "Both runners did work, so the two ran at the same time.");
        foreach (var (tenantId, key) in tenants)
        {
            (await KeysUnderAsync(key)).ShouldBeEmpty();
            (await RequestsOfTenantAsync(tenantId)).ShouldBeEmpty();
        }
    }

    [Test]
    public async Task the_delete_of_a_tenant_keeps_the_objects_of_another_tenant()
    {
        // Arrange
        var (deleted, admin) = await AddTenantAsync();
        await new TestingServiceScope().StartGraph(out _).AddTenant(out var kept).BuildAsync();
        await PutAsync($"tenants/{deleted}/files/{Guid.NewGuid():N}");
        await PutAsync($"tenants/{kept.Id}/files/{Guid.NewGuid():N}");
        await PutAsync($"tenants/{kept.Id}/no-row/{Guid.NewGuid():N}");
        await using var storageUp = WithStorageUp();

        // Act
        await DeleteTenantAsync(deleted, admin, storageUp);
        await RunDueAsync(storageUp, DateTimeOffset.UtcNow);

        // Assert
        (await KeysUnderAsync($"tenants/{deleted}/")).ShouldBeEmpty();
        (await KeysUnderAsync($"tenants/{kept.Id}/")).Count.ShouldBe(2);
        (await RequestsOfTenantAsync(kept.Id)).ShouldBeEmpty();
    }

    private static List<ObjectDeleteOutcome> Of(string requestId, IReadOnlyList<ObjectDeleteAttempt> attempts)
        => [.. attempts.Where(a => a.RequestId == requestId).Select(a => a.Outcome)];

    /// <summary>Deletes a new tenant that has one object in storage, and gives the tenant and the key of the object.</summary>
    private static async Task<(string TenantId, string Key)> DeleteTenantWithObjectAsync()
    {
        var (tenantId, admin) = await AddTenantAsync();
        var key = $"tenants/{tenantId}/files/{Guid.NewGuid():N}";
        await PutAsync(key);
        await DeleteTenantAsync(tenantId, admin);
        return (tenantId, key);
    }

    /// <summary>The <c>object_delete.run</c> spans. Each run is the root of a trace of its own, so this finds them by name.</summary>
    private sealed class RunSpans : IDisposable
    {
        private readonly ConcurrentQueue<Activity> _spans = new();
        private readonly ActivityListener _listener;

        public RunSpans()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == Telemetry.ServiceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => options.Name == "object_delete.run"
                    ? ActivitySamplingResult.AllDataAndRecorded
                    : ActivitySamplingResult.None,
                ActivityStopped = activity =>
                {
                    if (activity.OperationName == "object_delete.run")
                        _spans.Enqueue(activity);
                },
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public List<Activity> Of(string requestId)
            => [.. _spans.Where(s => Equals(s.GetTagItem("app.object_delete_request_id"), requestId))];

        public void Dispose() => _listener.Dispose();
    }
}
