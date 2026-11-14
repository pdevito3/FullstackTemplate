namespace FullstackTemplate.Server.Resources.Jobs;

using BackWave.Jobs;
using Domain.Users;

/// <summary>
/// The base of the handler of an <see cref="ISystemJob"/>. As <see cref="JobHandler{TJob}"/> does, with the caller
/// <see cref="CallerIdentity.System"/>, which sees the rows of all tenants.
/// </summary>
public abstract class SystemJobHandler<TJob>(JobScope scope) : IJobHandler<TJob> where TJob : class, ISystemJob
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
