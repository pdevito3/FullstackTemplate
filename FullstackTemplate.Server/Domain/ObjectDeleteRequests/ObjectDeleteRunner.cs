namespace FullstackTemplate.Server.Domain.ObjectDeleteRequests;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Databases;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Resources;
using Resources.Storage;
using Serilog;

/// <summary>
/// Does the object deletes that are due. It claims one request at a time with <c>FOR UPDATE SKIP LOCKED</c>, so two
/// Server instances never claim the same request at the same time. A delete of an object that is gone succeeds, so a
/// request that runs two times, for example after a runner stopped during an attempt, does no harm.
/// </summary>
public sealed class ObjectDeleteRunner(
    AppDbContext dbContext,
    ObjectStorage storage,
    IOptions<ObjectDeleteOptions> options,
    TimeProvider timeProvider)
{
    private readonly ILogger _logger = Log.ForContext<ObjectDeleteRunner>();

    /// <summary>
    /// Claims and does each request that is due, and gives the outcome of each. It reads the clock at each claim and
    /// each failure, not one time for the run, because an attempt can take up to <c>AttemptTimeout</c>.
    /// </summary>
    public async Task<IReadOnlyList<ObjectDeleteAttempt>> RunDueAsync(CancellationToken cancellationToken)
    {
        var attempts = new List<ObjectDeleteAttempt>();
        while (await ClaimNextAsync(timeProvider.GetUtcNow(), cancellationToken) is { } request)
            attempts.Add(await RunAsync(request, cancellationToken));
        return attempts;
    }

    // An idle run starts no trace, so the claim query of a run with nothing due exports no span
    private async Task<ObjectDeleteAttempt> RunAsync(ObjectDeleteRequest request, CancellationToken cancellationToken)
    {
        // An attempt is a trace of its own, as it can run hours after the delete. A link, not a parent, leads to the delete.
        ActivityLink[] links = Telemetry.TryParseTraceContext(new TraceContext(request.TraceParent, request.TraceState), out var save)
            ? [new ActivityLink(save)]
            : [];
        using var trace = Telemetry.TraceRoot(
            [
                ("app.object_delete_request_id", request.Id),
                ("app.tenant_id", request.TenantId),
                ("app.object_delete.is_prefix", request.IsPrefix),
                ("app.object_delete.attempt", request.Attempts),
            ],
            "object_delete.run",
            links);

        var outcome = await DeleteObjectsAsync(request, cancellationToken);
        trace.Activity?.SetTag("app.object_delete.outcome", ObjectDeleteMetrics.NameOf(outcome.Outcome));
        ObjectDeleteMetrics.Record(outcome.Outcome);
        return outcome;
    }

    private async Task<ObjectDeleteAttempt> DeleteObjectsAsync(ObjectDeleteRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasAttemptLeft(options.Value))
            return await FailAsync(request, "The runner stopped during the last attempt.", null, cancellationToken);

        int objectCount;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.Value.AttemptTimeout);
            objectCount = request.IsPrefix
                ? await storage.DeleteUnderAsync(request.Key, timeout.Token)
                : await DeleteOneAsync(request.Key, timeout.Token);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return await FailAsync(request, $"{ex.GetType().FullName}: {ex.Message}", ex, cancellationToken);
        }

        var completed = await CompleteAsync(request, cancellationToken);
        _logger.Here().Information(
            "Deleted {ObjectCount} objects of object delete request {ObjectDeleteRequestId} of tenant {TenantId} in attempt {Attempt}",
            objectCount, request.Id, request.TenantId, request.Attempts);
        return new ObjectDeleteAttempt(request.Id, completed ? ObjectDeleteOutcome.Deleted : ObjectDeleteOutcome.LostClaim);
    }

    private async Task<int> DeleteOneAsync(string key, CancellationToken cancellationToken)
    {
        await storage.DeleteAsync(key, cancellationToken);
        return 1;
    }

    // The delay starts at the end of the attempt, so an attempt that ran to its timeout still waits the whole delay
    private async Task<ObjectDeleteAttempt> FailAsync(ObjectDeleteRequest request, string error, Exception? exception,
        CancellationToken cancellationToken)
    {
        var failure = request.AfterFailure(error, timeProvider.GetUtcNow(), options.Value);
        var recorded = await RecordFailureAsync(request, failure, cancellationToken);
        if (!recorded)
            return new ObjectDeleteAttempt(request.Id, ObjectDeleteOutcome.LostClaim);

        if (failure.IsDead)
        {
            Telemetry.RecordError(Activity.Current, exception ?? new InvalidOperationException(error));
            _logger.Here().Error(exception,
                "Object delete request {ObjectDeleteRequestId} of tenant {TenantId} is dead after {Attempt} attempts, so its objects stay until a person runs it again",
                request.Id, request.TenantId, request.Attempts);
            return new ObjectDeleteAttempt(request.Id, ObjectDeleteOutcome.Dead);
        }

        Telemetry.AddEvent([("exception.type", exception?.GetType().FullName)], "object_delete.failed");
        _logger.Here().Warning(exception,
            "Could not delete the objects of object delete request {ObjectDeleteRequestId} of tenant {TenantId} in attempt {Attempt}. The next attempt is at {DueAt}",
            request.Id, request.TenantId, request.Attempts, failure.DueAt);
        return new ObjectDeleteAttempt(request.Id, ObjectDeleteOutcome.RetryLater);
    }

    // The claim counts the attempt before the work, so an attempt whose runner stops still counts. It moves the due
    // time past the end of the attempt, so another runner takes the request only after this one could have ended.
    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
        Justification = "object_delete_requests is [NotAudited], so this write needs no audit entry.")]
    private async Task<ObjectDeleteRequest?> ClaimNextAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var claimEnd = now + options.Value.AttemptTimeout * 2;
        var claimed = await dbContext.Set<ObjectDeleteRequest>()
            .FromSql($"""
                UPDATE object_delete_requests AS r
                SET attempts = r.attempts + 1, due_at = {claimEnd}
                WHERE r.id = (
                    SELECT id FROM object_delete_requests
                    WHERE dead_at IS NULL AND due_at <= {now}
                    ORDER BY due_at, id
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED)
                RETURNING r.*
                """)
            // The runner works for each tenant, and the tenant filter would also wrap the UPDATE in a SELECT
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .TagWithOperationCallSite("object_delete_requests.claim")
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return claimed.SingleOrDefault();
    }

    // Each write after the claim matches the attempt count of the claim. When another runner claimed the request
    // after this claim ended, the write changes no row, and the state of that runner stays.
    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
        Justification = "object_delete_requests is [NotAudited], so this write needs no audit entry.")]
    private async Task<bool> CompleteAsync(ObjectDeleteRequest request, CancellationToken cancellationToken)
        => await dbContext.Set<ObjectDeleteRequest>()
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .TagWithOperationCallSite("object_delete_requests.complete")
            .Where(r => r.Id == request.Id && r.Attempts == request.Attempts)
            .ExecuteDeleteAsync(cancellationToken) == 1;

    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
        Justification = "object_delete_requests is [NotAudited], so this write needs no audit entry.")]
    private async Task<bool> RecordFailureAsync(ObjectDeleteRequest request, ObjectDeleteFailure failure,
        CancellationToken cancellationToken)
        => await dbContext.Set<ObjectDeleteRequest>()
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .TagWithOperationCallSite("object_delete_requests.record_failure")
            .Where(r => r.Id == request.Id && r.Attempts == request.Attempts)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.DueAt, failure.DueAt)
                .SetProperty(r => r.DeadAt, failure.DeadAt)
                .SetProperty(r => r.LastError, failure.LastError), cancellationToken) == 1;
}

public enum ObjectDeleteOutcome
{
    Deleted,
    RetryLater,
    Dead,

    /// <summary>Another runner claimed the request after the claim of this attempt ended, so this attempt wrote nothing.</summary>
    LostClaim,
}

public sealed record ObjectDeleteAttempt(string RequestId, ObjectDeleteOutcome Outcome);
