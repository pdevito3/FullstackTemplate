namespace FullstackTemplate.Server.Resources.Jobs;

using BackWave.Generated;
using BackWave.Hosting;
using BackWave.Pro;
using BackWave.Pro.Mcp;
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
    /// <c>/health</c>. With <paramref name="jobsMcp"/>, it also adds the BackWave tools to the MCP server, with the gates
    /// that <paramref name="jobsMcp"/> sets.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A setting of <see cref="ObjectDeleteOptions"/> or <see cref="ObjectSweepOptions"/> is out of its range.
    /// </exception>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration,
        ServerRole role, Action<BackWaveProMcpOptions>? jobsMcp = null)
    {
        var deletes = ObjectDeleteOptions.From(configuration);
        services.AddSingleton(Options.Create(deletes));
        services.AddSingleton(Options.Create(ObjectSweepOptions.From(configuration)));

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
            if (jobsMcp is not null)
                backWave.AddMcp(jobsMcp);
        });
        // A worker group that stops on a fault of the store makes the Server unhealthy, so that a person sees it
        if (role.RunsJobs())
            services.AddHealthChecks().AddCheck<BackWaveHealthCheck>("backwave");
        // Only the jobs tools of MCP use BackWave Pro, so a process without them does not take on its license
        if (jobsMcp is not null)
            services.AddBackWavePro(configuration["BackWave:ProLicense"]);
        services.AddSingleton<IJobWriter, JobWriter>();
        services.AddScoped<JobScope>();
        services.Configure<BackgroundJobsOptions>(configuration.GetSection(BackgroundJobsOptions.SectionName));
        services.AddScoped<JobOperators>();
        return services;
    }
}
