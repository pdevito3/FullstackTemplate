namespace FullstackTemplate.Server.Resources.Jobs;

using BackWave.Hosting;
using BackWave.Jobs;
using Databases;

public static class BackgroundJobsRegistration
{
    /// <summary>
    /// Adds BackWave over its schema in appdb: the store, the job registry, the client, the monitor, the writer of
    /// the jobs that a save queues, and the scope of a job attempt.
    /// </summary>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        services.AddBackWave(backWave => backWave
            .UseStore(provider => BackWaveSchema.CreateStore(provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<ILoggerFactory>()))
            .UseRegistry(new JobRegistry([])));
        services.AddSingleton<IJobWriter, JobWriter>();
        services.AddScoped<JobScope>();
        return services;
    }
}
