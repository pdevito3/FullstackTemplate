namespace FullstackTemplate.Server.Domain.ObjectDeletes;

using System.Diagnostics;
using Attachments;
using BackWave.Jobs;
using Databases;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Resources;
using Resources.Jobs;
using Resources.Storage;
using Serilog;

/// <summary>
/// Deletes each object of the bucket whose verdict is <see cref="SweepVerdict.Orphan"/>. An add that stops
/// between its put and its save leaves such an object, and so does a put that lands after the delete of the objects of
/// its tenant. <see cref="ObjectSweepSchedule"/> runs it.
/// </summary>
[Job("sweep-objects", Queue = ObjectSweepOptions.Queue)]
public sealed record SweepObjects : ISystemJob;

/// <summary>
/// Lists the bucket one page at a time, and deletes the orphans of each page. A delete of an object that is gone
/// succeeds, so a second run does no harm. A failure throws, and BackWave runs the job again.
/// </summary>
public sealed class SweepObjectsHandler(
    JobScope scope,
    AppDbContext dbContext,
    ObjectStorage storage,
    TimeProvider timeProvider,
    IOptions<ObjectSweepOptions> sweepOptions,
    IOptions<AttachmentOptions> attachmentOptions,
    IOptions<ObjectDeleteOptions> deleteOptions)
    : SystemJobHandler<SweepObjects>(scope)
{
    private static readonly ILogger Logger = Log.ForContext<SweepObjectsHandler>();

    protected override async Task RunAsync(SweepObjects job, JobContext context, CancellationToken cancellationToken)
    {
        // Read before the list and the rows. An add that puts its object after this time is young in this run, so a row
        // that this run cannot see names no object that it deletes.
        var now = timeProvider.GetUtcNow();
        var grace = sweepOptions.Value.GraceFor(attachmentOptions.Value);
        if (await UnknownMigrationsAsync(cancellationToken) is { Count: > 0 } unknown)
        {
            Logger.Here().Warning(
                "Skipped the sweep of the bucket, because this Server does not know the migrations {Migrations} of the database. Their storage key columns can name objects that this Server cannot see",
                unknown);
            return;
        }

        var activity = Activity.Current;
        var (listed, kept, ofUnknownTenants, deleted) = (0, 0, 0, 0);
        var (refusedCount, someRefused) = (0, new List<string>());
        string? continuationToken = null;
        do
        {
            var page = await ObjectDeletes.InAttemptTimeAsync(deleteOptions.Value.AttemptTimeout,
                attempt => storage.ListPageAsync(StoredFile.TenantsPrefix, continuationToken, attempt), cancellationToken);
            var decision = await DecideAsync(page.Objects, now, grace, cancellationToken);
            // A key that storage refuses to delete, as with Object Lock or a deny policy, does not stop the pages after it.
            var refusedOfPage = decision.Orphans.Count > 0
                ? await ObjectDeletes.InAttemptTimeAsync(deleteOptions.Value.AttemptTimeout,
                    attempt => storage.DeleteAsync(decision.Orphans, attempt), cancellationToken)
                : [];
            if (refusedOfPage.Count > 0)
                Logger.Here().Error(
                    "Storage refused the delete of {RefusedCount} objects that no row names, such as {Keys}. The sweep goes on to the next page",
                    refusedOfPage.Count, refusedOfPage.Take(10));
            refusedCount += refusedOfPage.Count;
            someRefused.AddRange(refusedOfPage.Take(10 - someRefused.Count));
            ObjectDeleteMetrics.Swept.Add(decision.Orphans.Count - refusedOfPage.Count);

            listed += page.Objects.Count;
            kept += decision.Kept;
            ofUnknownTenants += decision.OfUnknownTenants;
            deleted += decision.Orphans.Count - refusedOfPage.Count;
            activity?.SetTag("app.object_sweep.listed_count", listed);
            activity?.SetTag("app.object_sweep.kept_count", kept);
            activity?.SetTag("app.object_sweep.deleted_count", deleted);
            continuationToken = page.NextToken;
        } while (continuationToken is not null);

        if (ofUnknownTenants > 0)
            Logger.Here().Warning(
                "Kept {ObjectCount} objects whose keys name no tenant of this database. Another database can use this bucket. Give each database its own bucket",
                ofUnknownTenants);
        Logger.Here().Information(
            "Swept the bucket: listed {ListedCount} objects, kept {KeptCount}, and deleted {DeletedCount} that no row names",
            listed, kept, deleted);
        // The run fails, so BackWave runs it again, and the delete of a key that is gone succeeds.
        if (refusedCount > 0)
            throw new InvalidOperationException(
                $"Storage refused the delete of {refusedCount} objects that no row names, such as {string.Join(", ", someRefused)}.");
    }

    private async Task<SweepPage> DecideAsync(IReadOnlyList<StoredObject> objects, DateTimeOffset now, TimeSpan grace,
        CancellationToken cancellationToken)
    {
        if (objects.Count == 0)
            return new SweepPage([], 0, 0);

        var keys = objects.Select(o => o.Key).ToList();
        var tenantIds = keys.Select(ObjectSweep.TenantOf).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var tenants = await dbContext.Tenants
            .TagWithOperationCallSite("object_sweep.load_tenants")
            .IgnoreQueryFilters([QueryFilterNames.SoftDelete])
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        var namedKeys = await NamedStorageKeys.AmongAsync(dbContext, keys, cancellationToken);
        return ObjectSweep.Decide(objects, tenants.ToHashSet(StringComparer.Ordinal), namedKeys, now, grace);
    }

    /// <summary>
    /// The migrations of the database that this Server does not have, as when a newer Server migrated the database
    /// during a deploy.
    /// </summary>
    private async Task<List<string>> UnknownMigrationsAsync(CancellationToken cancellationToken)
    {
        var known = dbContext.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        return [.. (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).Where(m => !known.Contains(m))];
    }
}
