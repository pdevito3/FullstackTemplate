namespace FullstackTemplate.IntegrationTests.Jobs;

using System.Text.Json.Serialization;
using BackWave.Jobs;
using BackWave.Monitor;
using FullstackTemplate.Server.Resources.Jobs;

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
    public static Task<IReadOnlyList<JobSnapshot>> OfTenantAsync(string tenantId)
        => StoredJobs.OfTenantAsync<TestJob>(tenantId);
}

[JsonSerializable(typeof(TestJob))]
internal sealed partial class TestJobJson : JsonSerializerContext;
