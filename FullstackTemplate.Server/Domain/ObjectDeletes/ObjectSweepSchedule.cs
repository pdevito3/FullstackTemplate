namespace FullstackTemplate.Server.Domain.ObjectDeletes;

using System.Diagnostics.CodeAnalysis;
using BackWave;
using BackWave.Storage;
using Microsoft.Extensions.Options;

/// <summary>
/// Defines the schedule of <see cref="SweepObjects"/>. The start of each Server defines it again with its settings. A
/// schedule that exists keeps its cursor, so a tick that the Servers missed while they were down runs one time.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
    Justification = "A schedule is not a job of a save, and only BackWaveClient defines a schedule.")]
internal static class ObjectSweepSchedule
{
    public const string ScheduleId = "sweep-objects";

    public static async Task UpsertAsync(IServiceProvider services, CancellationToken cancellationToken)
        => await services.GetRequiredService<BackWaveClient>().UpsertRecurringAsync(ScheduleId,
            services.GetRequiredService<IOptions<ObjectSweepOptions>>().Value.Cron, new SweepObjects(),
            catchUp: CatchUpPolicy.Coalesce, noOverlap: true, cancellationToken: cancellationToken);
}
