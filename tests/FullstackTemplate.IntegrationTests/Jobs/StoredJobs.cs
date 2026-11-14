namespace FullstackTemplate.IntegrationTests.Jobs;

using System.Text;
using BackWave.Jobs;
using BackWave.Monitor;
using BackWave.Storage;
using FullstackTemplate.Server.Resources.Jobs;
using Microsoft.Extensions.DependencyInjection;

/// <summary>The jobs that the saves of the tests commit to the job store of the database. No worker runs them.</summary>
public static class StoredJobs
{
    /// <summary>The jobs of <typeparamref name="TJob"/> of the tenant, oldest first.</summary>
    public static async Task<IReadOnlyList<JobSnapshot>> OfTenantAsync<TJob>(string tenantId)
    {
        await using var scope = TestFixture.BaseScopeFactory.CreateAsyncScope();
        return await ListAsync<TJob>(scope.ServiceProvider, tenantId);
    }

    /// <summary>The payloads of the jobs of <typeparamref name="TJob"/> of the tenant, oldest first.</summary>
    public static async Task<IReadOnlyList<TJob>> QueuedAsync<TJob>(string tenantId)
    {
        await using var scope = TestFixture.BaseScopeFactory.CreateAsyncScope();
        var monitor = scope.ServiceProvider.GetRequiredService<BackWaveMonitor>();
        var registration = RegistrationOf<TJob>(scope.ServiceProvider);
        List<TJob> jobs = [];
        foreach (var job in await ListAsync<TJob>(scope.ServiceProvider, tenantId))
        {
            var payload = await monitor.GetJobPayloadAsync(job.JobId)
                ?? throw new InvalidOperationException($"Job {job.JobId} has no payload.");
            jobs.Add((TJob)registration.Deserialize(Encoding.UTF8.GetBytes(payload.Text)));
        }
        return jobs;
    }

    public static JobRegistration RegistrationOf<TJob>(IServiceProvider services)
        => services.GetRequiredService<JobRegistry>().Registrations.Single(r => r.JobType == typeof(TJob));

    private static async Task<IReadOnlyList<JobSnapshot>> ListAsync<TJob>(IServiceProvider services, string tenantId)
        => await services.GetRequiredService<BackWaveMonitor>().ListJobsAsync(new JobQuery
        {
            WireName = RegistrationOf<TJob>(services).WireName,
            TagPredicates = [JobTagPredicate.HasKeyValue(PendingJob.TenantTag, tenantId)],
        });
}
