namespace FullstackTemplate.Server.Domain.ObjectDeletes;

using Attachments;
using BackWave.Jobs;
using Microsoft.Extensions.Options;
using Resources;
using Resources.Jobs;
using Resources.Storage;
using Serilog;

/// <summary>Deletes the object in storage of a deleted attachment.</summary>
[Job("delete-object", Queue = ObjectDeleteOptions.Queue)]
public sealed record DeleteObject : ITenantJob
{
    /// <exception cref="ArgumentException">The key is not under the prefix of the tenant.</exception>
    public DeleteObject(string tenantId, string key)
    {
        // A key of another tenant would let a job of this tenant delete an object of that tenant. BackWave reads a
        // stored job with this constructor too, so it quarantines a job with such a key and does not run it.
        if (!ObjectDeletes.IsTenantId(tenantId) || key?.StartsWith(StoredFile.TenantPrefix(tenantId), StringComparison.Ordinal) != true)
            throw new ArgumentException(
                $"The key of an object delete of tenant {tenantId} must start with {StoredFile.TenantPrefix(tenantId)}.",
                nameof(key));

        TenantId = tenantId;
        Key = key;
    }

    public string TenantId { get; }

    public string Key { get; }
}

/// <summary>
/// Deletes the object of the job. A delete of an object that is gone succeeds, so a second run does no harm. A failure
/// throws, and BackWave runs the job again after a delay, then dead-letters it.
/// </summary>
public sealed class DeleteObjectHandler(JobScope scope, ObjectStorage storage, IOptions<ObjectDeleteOptions> options)
    : JobHandler<DeleteObject>(scope)
{
    private static readonly ILogger Logger = Log.ForContext<DeleteObjectHandler>();

    protected override async Task RunAsync(DeleteObject job, JobContext context, CancellationToken cancellationToken)
    {
        await ObjectDeletes.InAttemptTimeAsync(options.Value.AttemptTimeout, async attempt =>
        {
            await storage.DeleteAsync(job.Key, attempt);
            return 1;
        }, cancellationToken);
        Logger.Here().Information("Deleted the object of a deleted attachment of tenant {TenantId}", job.TenantId);
    }
}
