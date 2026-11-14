namespace FullstackTemplate.Server.Domain.ObjectDeletes;

using Attachments;
using BackWave.Jobs;
using Microsoft.Extensions.Options;
using Resources;
using Resources.Jobs;
using Resources.Storage;
using Serilog;

/// <summary>
/// Deletes each object under the prefix of a deleted tenant, also an object that no row names. The prefix comes from
/// the tenant ID only, so this job cannot delete the objects of another tenant.
/// </summary>
[Job("delete-tenant-objects", Queue = ObjectDeleteOptions.Queue)]
public sealed record DeleteTenantObjects : ITenantJob
{
    /// <exception cref="ArgumentException">The tenant ID is not a tenant ID.</exception>
    public DeleteTenantObjects(string tenantId)
    {
        if (!ObjectDeletes.IsTenantId(tenantId))
            throw new ArgumentException($"'{tenantId}' is not a tenant ID.", nameof(tenantId));

        TenantId = tenantId;
    }

    public string TenantId { get; }
}

/// <summary>
/// Deletes the objects of the tenant. A delete of objects that are gone succeeds, so a second run does no harm. A
/// failure throws, and BackWave runs the job again after a delay, then dead-letters it.
/// </summary>
public sealed class DeleteTenantObjectsHandler(JobScope scope, ObjectStorage storage, IOptions<ObjectDeleteOptions> options)
    : JobHandler<DeleteTenantObjects>(scope)
{
    private static readonly ILogger Logger = Log.ForContext<DeleteTenantObjectsHandler>();

    protected override async Task RunAsync(DeleteTenantObjects job, JobContext context, CancellationToken cancellationToken)
    {
        var count = await ObjectDeletes.InAttemptTimeAsync(options.Value.AttemptTimeout,
            attempt => storage.DeleteUnderAsync(StoredFile.TenantPrefix(job.TenantId), attempt), cancellationToken);
        Logger.Here().Information("Deleted {ObjectCount} objects of deleted tenant {TenantId}", count, job.TenantId);
    }
}
