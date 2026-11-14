namespace FullstackTemplate.Server.Databases;

using System.Diagnostics.CodeAnalysis;
using Domain.Attachments;
using Domain.ObjectDeletes;
using Npgsql;
using Resources;
using Resources.Jobs;
using Serilog;

/// <summary>
/// Moves the rows of the old object delete queue, <c>object_delete_requests</c>, onto BackWave, before the migration that
/// drops the queue. Each row names objects in storage that nothing else deletes. A later template release removes this
/// drain.
/// </summary>
/// <remarks>
/// <see cref="MigrationHostedService"/> runs it in the migration lock, after the BackWave schema and before the EF Core
/// migrations. The delete of the rows and the write of their jobs commit together, so a failed start moves no row. A
/// dead row gets a job too. A Server of the previous version can add a row after the drain, for example in a rolling
/// deploy. The drop migration then refuses to drop the queue, and the next start drains it.
/// </remarks>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
    Justification = "object_delete_requests has no entity now and no audit, so only SQL reads and deletes its rows.")]
internal static class ObjectDeleteRequestsDrain
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ObjectDeleteRequestsDrain));

    public static async Task DrainAsync(string connectionString, IJobWriter jobWriter, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (!await QueueExistsAsync(connection, transaction, cancellationToken))
            return;

        var requests = await DeleteRequestsAsync(connection, transaction, cancellationToken);
        if (requests.Count == 0)
            return;

        List<PendingJob> jobs = [];
        foreach (var request in requests)
        {
            if (JobOf(request) is { } job)
                jobs.Add(job);
            else
                Logger.Here().Warning(
                    "Dropped object delete request {ObjectDeleteRequestId} of tenant {TenantId}, because its key {StorageKey} is not under the tenant. If the object exists, delete it by hand",
                    request.Id, request.TenantId, request.Key);
        }

        if (jobs.Count > 0)
            await jobWriter.WriteAsync(jobs, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        Logger.Here().Information(
            "Moved {JobCount} of {RequestCount} object delete requests onto BackWave", jobs.Count, requests.Count);
    }

    /// <summary>
    /// The job of one row, or null when the row fails the check of its job. A prefix row deletes the objects of its tenant,
    /// so a prefix that is not the prefix of the tenant gets no job, because that job deletes more.
    /// </summary>
    private static PendingJob? JobOf(Request request)
    {
        try
        {
            if (!request.IsPrefix)
                return PendingJob.Of(new DeleteObject(request.TenantId, request.Key));
            return request.Key == StoredFile.TenantPrefix(request.TenantId)
                ? PendingJob.Of(new DeleteTenantObjects(request.TenantId))
                : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static async Task<bool> QueueExistsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT to_regclass('object_delete_requests') IS NOT NULL", connection, transaction);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<IReadOnlyList<Request>> DeleteRequestsAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "DELETE FROM object_delete_requests RETURNING id, tenant_id, key, is_prefix", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        List<Request> requests = [];
        while (await reader.ReadAsync(cancellationToken))
            requests.Add(new Request(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)));
        return requests;
    }

    /// <summary>A row of the old queue. With <paramref name="IsPrefix"/>, the key is the start of the key of each object.</summary>
    private sealed record Request(string Id, string TenantId, string Key, bool IsPrefix);
}
