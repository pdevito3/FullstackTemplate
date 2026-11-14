namespace FullstackTemplate.IntegrationTests.Jobs;

using System.Text.Json.Serialization;
using BackWave.Jobs;
using BackWave.Monitor;
using BackWave.Storage;
using FullstackTemplate.Server.Resources.Jobs;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A job of the tests. The fixture adds its registration to the job registry, and no worker runs it, so each test job
/// that a save commits stays pending.
/// </summary>
public sealed record TestJob(string TenantId, string Note) : ITenantJob
{
    public const string WireName = "test-job";

    public static JobRegistration Registration { get; } =
        JobRegistration.Create<TestJob, Handler>(WireName, TestJobJson.Default.TestJob);

    public sealed class Handler : IJobHandler<TestJob>
    {
        public Task HandleAsync(TestJob job, JobContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    /// <summary>The test jobs of the tenant in the job store, oldest first.</summary>
    public static async Task<IReadOnlyList<JobSnapshot>> OfTenantAsync(string tenantId)
    {
        await using var scope = TestFixture.BaseScopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BackWaveMonitor>().ListJobsAsync(new JobQuery
        {
            WireName = WireName,
            TagPredicates = [JobTagPredicate.HasKeyValue(PendingJob.TenantTag, tenantId)],
        });
    }
}

[JsonSerializable(typeof(TestJob))]
internal sealed partial class TestJobJson : JsonSerializerContext;
