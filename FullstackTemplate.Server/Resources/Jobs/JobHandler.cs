namespace FullstackTemplate.Server.Resources.Jobs;

using BackWave.Jobs;

/// <summary>
/// The base of each job handler. BackWave resolves the handler in a new DI scope for each attempt. The base runs the
/// job as the tenant of its payload, names the audit request after the job, and refuses work that the job did not save.
/// </summary>
public abstract class JobHandler<TJob>(JobScope scope) : IJobHandler<TJob> where TJob : class, ITenantJob
{
    public async Task HandleAsync(TJob job, JobContext context, CancellationToken cancellationToken)
    {
        using (scope.Enter(job))
        {
            await RunAsync(job, context, cancellationToken);
            scope.RefuseUnsavedWork(job);
        }
    }

    /// <summary>
    /// The work of the job. BackWave runs it again after a failure or a lost lease, so make it idempotent.
    /// </summary>
    protected abstract Task RunAsync(TJob job, JobContext context, CancellationToken cancellationToken);
}
