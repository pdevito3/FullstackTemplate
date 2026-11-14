namespace FullstackTemplate.Server.Resources.Jobs;

using BackWave.Core;
using BackWave.Hosting;
using BackWave.Jobs;
using Domain.ObjectDeletes;

internal static class JobGroups
{
    /// <summary>
    /// One worker group for each queue of the module, so that a slow queue does not hold up another. The list comes
    /// from the jobs, so a job on a new queue also gets a group.
    /// </summary>
    public static IEnumerable<WorkerGroupOptions> For(JobModule module, ObjectDeleteOptions deletes) =>
        module.Registrations.Select(r => r.Queue).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(queue => queue == ObjectDeleteOptions.Queue
                ? deletes.WorkerGroup()
                : new WorkerGroupOptions { Name = queue, Policy = new DispatchPolicy.Strict(queue) });
}
