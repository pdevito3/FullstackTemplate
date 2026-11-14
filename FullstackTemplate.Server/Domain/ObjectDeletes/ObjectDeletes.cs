namespace FullstackTemplate.Server.Domain.ObjectDeletes;

using Attachments;
using Resources.Jobs;
using Tenants;

/// <summary>
/// The objects in storage go after the commit of the rows that name them. Each save queues the jobs that delete the
/// objects of its deleted rows, so the jobs commit with the delete, or neither commits.
/// </summary>
public static class ObjectDeletes
{
    /// <summary>
    /// The jobs for the deleted rows of one save: one for each deleted attachment, and one for all the objects of each
    /// deleted tenant. The job of a tenant covers the attachments of the tenant and each object that no row names, so
    /// an attachment of a deleted tenant gets no job of its own.
    /// </summary>
    /// <exception cref="ArgumentException">The key of an attachment is not under the prefix of its tenant.</exception>
    internal static IReadOnlyList<PendingJob> ForDeleted(IEnumerable<object> deletedEntities)
    {
        var deleted = deletedEntities.ToList();
        var tenants = deleted.OfType<Tenant>().Select(t => t.Id).ToHashSet();
        return
        [
            .. tenants.Select(tenantId => PendingJob.Of(new DeleteTenantObjects(tenantId))),
            .. deleted.OfType<IAttachment>()
                .Where(a => !tenants.Contains(a.TenantId))
                .Select(a => PendingJob.Of(new DeleteObject(a.TenantId, a.File.StorageKey))),
        ];
    }

    internal static bool IsTenantId(string? tenantId) => tenantId is not null && EntityId.IsValid(tenantId, Tenant.IdPrefix);

    /// <summary>Runs the storage call of one attempt, and stops it after <paramref name="timeout"/>.</summary>
    /// <exception cref="TimeoutException">The call took longer than <paramref name="timeout"/>.</exception>
    internal static async Task<int> InAttemptTimeAsync(TimeSpan timeout, Func<CancellationToken, Task<int>> delete,
        CancellationToken cancellationToken)
    {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(timeout);
        try
        {
            return await delete(attempt.Token);
        }
        // The failure detail of the job then names the timeout, not "A task was canceled". A cancel of BackWave, such as
        // a shutdown, stays a cancel, and BackWave gives the job back with no failed attempt.
        catch (OperationCanceledException exception)
            when (attempt.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The object delete took more than {timeout}.", exception);
        }
    }
}
