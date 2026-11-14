namespace FullstackTemplate.Server.Resources.Jobs;

using BackWave.Generated;
using BackWave.Hosting;
using Databases;
using Domain.ObjectDeletes;
using Microsoft.Extensions.Options;

public static class BackgroundJobsRegistration
{
    /// <summary>
    /// Adds BackWave over its schema in appdb: the store, the jobs of <see cref="BackWaveJobs.Module"/> and their
    /// handlers, one worker group for each queue, the client, the monitor, the writer of the jobs that a save queues,
    /// and the scope of a job attempt.
    /// </summary>
    /// <exception cref="InvalidOperationException">A setting of <see cref="ObjectDeleteOptions"/> is out of its range.</exception>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        var deletes = ObjectDeleteOptions.From(configuration);
        services.AddSingleton(Options.Create(deletes));

        services.AddBackWave(backWave =>
        {
            backWave.UseStore(provider => BackWaveSchema.CreateStore(provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<ILoggerFactory>()))
                .UseJobs(BackWaveJobs.Module);
            foreach (var group in JobGroups.For(BackWaveJobs.Module, deletes))
                backWave.AddWorkerGroup(group);
        });
        services.AddSingleton<IJobWriter, JobWriter>();
        services.AddScoped<JobScope>();
        return services;
    }
}
