namespace FullstackTemplate.Server.Resources.Jobs;

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using BackWave;
using BackWave.Monitor;
using BackWave.Storage;
using Databases;

/// <summary>Writes the queued jobs of a save on the connection of the save transaction.</summary>
public interface IJobWriter
{
    /// <returns>The ID of the first job, so that the save can look for its commit after a lost reply.</returns>
    Task<Guid> WriteAsync(IReadOnlyList<PendingJob> jobs, DbTransaction transaction, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid jobId, CancellationToken cancellationToken);
}

/// <summary>
/// A job that waits for the commit of its save. <see cref="AppDbContext.QueueJob{TJob}"/> makes one, and so does the save
/// for the object deletes of its deleted rows.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
    Justification = "The save writes each queued job in its own transaction, so that the job commits with the rows.")]
public sealed class PendingJob
{
    /// <summary>The key of the tag that holds the tenant of the job. The dashboard filters on it.</summary>
    public const string TenantTag = "tenant";

    private readonly Func<BackWaveClient, DbTransaction, DateTimeOffset, CancellationToken, ValueTask<Guid>> _enqueue;

    private PendingJob(ITenantJob job,
        Func<BackWaveClient, DbTransaction, DateTimeOffset, CancellationToken, ValueTask<Guid>> enqueue)
        => (Job, _enqueue) = (job, enqueue);

    internal ITenantJob Job { get; }

    // BackWave finds the registration of a job by its static type, so the closure keeps TJob. The call site of the
    // queue goes on the send span, not the call site in this file.
    internal static PendingJob Of<TJob>(TJob job,
        [CallerFilePath] string callerFilePath = "",
        [CallerMemberName] string callerMemberName = "",
        [CallerLineNumber] int callerLineNumber = 0)
        where TJob : class, ITenantJob
        => new(job, (client, transaction, now, ct) => client.EnqueueAsync(job, now,
            tags: JobTags.From([JobTag.Keyed(TenantTag, job.TenantId)]), transaction: transaction,
            cancellationToken: ct, callerFilePath: callerFilePath, callerMemberName: callerMemberName,
            callerLineNumber: callerLineNumber));

    internal ValueTask<Guid> EnqueueAsync(BackWaveClient client, DbTransaction transaction, DateTimeOffset now,
        CancellationToken cancellationToken) => _enqueue(client, transaction, now, cancellationToken);
}

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
    Justification = "The save writes each queued job in its own transaction, so that the job commits with the rows.")]
internal sealed class JobWriter(BackWaveClient client, BackWaveMonitor monitor, TimeProvider timeProvider) : IJobWriter
{
    public async Task<Guid> WriteAsync(IReadOnlyList<PendingJob> jobs, DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        Guid? first = null;
        // One at a time, because the jobs share the one connection of the transaction.
        foreach (var job in jobs)
        {
            var id = await job.EnqueueAsync(client, transaction, now, cancellationToken);
            first ??= id;
        }

        return first ?? throw new ArgumentException("A save writes jobs only when it has a queued job.", nameof(jobs));
    }

    public async Task<bool> ExistsAsync(Guid jobId, CancellationToken cancellationToken)
        => await monitor.GetJobAsync(jobId, cancellationToken) is not null;
}

/// <summary>The writer of a context that never saves a job, such as the context of the EF Core tools.</summary>
internal sealed class NoJobWriter : IJobWriter
{
    public static NoJobWriter Instance { get; } = new();

    private NoJobWriter()
    {
    }

    public Task<Guid> WriteAsync(IReadOnlyList<PendingJob> jobs, DbTransaction transaction,
        CancellationToken cancellationToken)
        => throw new InvalidOperationException("This AppDbContext cannot write jobs. Get AppDbContext from DI.");

    public Task<bool> ExistsAsync(Guid jobId, CancellationToken cancellationToken)
        => throw new InvalidOperationException("This AppDbContext cannot read jobs. Get AppDbContext from DI.");
}
