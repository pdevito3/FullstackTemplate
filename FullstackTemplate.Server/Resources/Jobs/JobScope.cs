namespace FullstackTemplate.Server.Resources.Jobs;

using System.Diagnostics;
using Databases;
using Domain.Tenants;
using Serilog.Context;
using Serilog.Core;
using Serilog.Core.Enrichers;

/// <summary>The DI scope of one job attempt. Only <see cref="JobHandler{TJob}"/> uses it.</summary>
public sealed class JobScope(ScopeGrant grant, AppDbContext dbContext)
{
    /// <summary>
    /// Grants the scope the tenant of the job, before the first query reads the caller. Puts the caller on the process
    /// span and on each log of the job, as <see cref="CallerIdentityBehavior{TRequest,TResponse}"/> does for a request.
    /// Each save of the job then has the job as its audit request.
    /// </summary>
    internal IDisposable Enter(ITenantJob job)
    {
        var properties = CallerTelemetry.Properties(grant.GrantTenantJob(job.TenantId));
        // BackWave makes the process span current while the handler runs.
        foreach (var (tag, _, value) in properties)
            Activity.Current?.SetTag(tag, value);

        ILogEventEnricher[] enrichers = [.. properties.Select(p => new PropertyEnricher(p.LogProperty, p.Value))];
        return new Entered(LogContext.Push(enrichers), dbContext.EnterRequest(job.GetType().Name));
    }

    /// <exception cref="InvalidOperationException">The job left a change, an audit action, or a job unsaved.</exception>
    internal void RefuseUnsavedWork(ITenantJob job)
    {
        if (dbContext.HasUnsavedJobs || dbContext.HasUnsavedActions || dbContext.ChangeTracker.HasChanges())
            throw new InvalidOperationException(
                $"{job.GetType().Name} changed data, recorded an audit action, or queued a job, and did not call SaveChangesAsync. Save it.");
    }

    private sealed class Entered(IDisposable logContext, IDisposable request) : IDisposable
    {
        public void Dispose()
        {
            request.Dispose();
            logContext.Dispose();
        }
    }
}
