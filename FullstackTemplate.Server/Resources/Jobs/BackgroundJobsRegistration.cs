namespace FullstackTemplate.Server.Resources.Jobs;

using BackWave.Generated;
using BackWave.Hosting;
using Databases;
using Domain.ObjectDeletes;
using Extensions;
using Microsoft.Extensions.Options;

public static class BackgroundJobsRegistration
{
    /// <summary>
    /// Adds BackWave over its schema in appdb: the store, the jobs of <see cref="BackWaveJobs.Module"/> and their
    /// handlers, the client, the monitor, the writer of the jobs that a save queues, the scope of a job attempt, and the
    /// operators of the jobs dashboard.
    /// When the <paramref name="role"/> runs jobs, it also adds one worker group for each queue, and their check in
    /// <c>/health</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">A setting of <see cref="ObjectDeleteOptions"/> is out of its range.</exception>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration,
        ServerRole role)
    {
        var deletes = ObjectDeleteOptions.From(configuration);
        services.AddSingleton(Options.Create(deletes));

        services.AddBackWave(backWave =>
        {
            backWave.UseStore(provider => BackWaveSchema.CreateStore(provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<ILoggerFactory>()))
                .UseJobs(BackWaveJobs.Module);
            if (role.RunsJobs())
            {
                foreach (var group in JobGroups.For(BackWaveJobs.Module, deletes))
                    backWave.AddWorkerGroup(group);
            }
        });
        // A worker group that stops on a fault of the store makes the Server unhealthy, so that a person sees it
        if (role.RunsJobs())
            services.AddHealthChecks().AddCheck<BackWaveHealthCheck>("backwave");
        services.AddSingleton<IJobWriter, JobWriter>();
        services.AddScoped<JobScope>();
        services.Configure<BackgroundJobsOptions>(configuration.GetSection(BackgroundJobsOptions.SectionName));
        services.AddScoped<JobOperators>();
        return services;
    }
}
