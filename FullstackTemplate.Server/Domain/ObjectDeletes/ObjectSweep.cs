namespace FullstackTemplate.Server.Domain.ObjectDeletes;

using Attachments;
using Resources.Storage;

/// <summary>What the sweep of the bucket does with one object.</summary>
public enum SweepVerdict
{
    /// <summary>The add of its row can still save the row, so the object stays.</summary>
    Young,

    /// <summary>The key names no tenant of this database, so the object can belong to another database. It stays.</summary>
    UnknownTenant,

    /// <summary>A live row names the object, so it stays.</summary>
    Named,

    /// <summary>No live row names the object, and no add can save one now. The sweep deletes it.</summary>
    Orphan,
}

/// <summary>The verdicts of one page of the bucket.</summary>
public sealed record SweepPage(IReadOnlyList<string> Orphans, int Kept, int OfUnknownTenants);

/// <summary>
/// The decision of the sweep of the bucket: an object goes when it is older than the grace, its key names a tenant of
/// this database, and no live row names it. An add saves its row within <see cref="AttachmentOptions.AddTimeout"/> of
/// its put, so no later save names an object older than the grace.
/// </summary>
public static class ObjectSweep
{
    /// <param name="stored">An object under <see cref="StoredFile.TenantsPrefix"/>.</param>
    /// <param name="tenants">The tenants of this database that the keys name, live or deleted.</param>
    /// <param name="namedKeys">The keys that the live rows name.</param>
    /// <param name="now">The time of the start of the sweep, read before the list and the rows.</param>
    /// <param name="grace">The age up to which an object stays with no row.</param>
    public static SweepVerdict VerdictOf(StoredObject stored, IReadOnlySet<string> tenants, IReadOnlySet<string> namedKeys,
        DateTimeOffset now, TimeSpan grace)
    {
        if (now - stored.LastModified <= grace)
            return SweepVerdict.Young;
        if (TenantOf(stored.Key) is not { } tenantId || !tenants.Contains(tenantId))
            return SweepVerdict.UnknownTenant;
        return namedKeys.Contains(stored.Key) ? SweepVerdict.Named : SweepVerdict.Orphan;
    }

    /// <summary>The verdict of each object of one page of the list.</summary>
    /// <inheritdoc cref="VerdictOf" path="/param"/>
    public static SweepPage Decide(IReadOnlyList<StoredObject> objects, IReadOnlySet<string> tenants,
        IReadOnlySet<string> namedKeys, DateTimeOffset now, TimeSpan grace)
    {
        var verdicts = objects.Select(o => (o.Key, Verdict: VerdictOf(o, tenants, namedKeys, now, grace))).ToList();
        return new SweepPage(
            [.. verdicts.Where(v => v.Verdict == SweepVerdict.Orphan).Select(v => v.Key)],
            verdicts.Count(v => v.Verdict != SweepVerdict.Orphan),
            verdicts.Count(v => v.Verdict == SweepVerdict.UnknownTenant));
    }

    /// <summary>The tenant that the key names, or null when the key is not under the prefix of a tenant ID.</summary>
    public static string? TenantOf(string key)
    {
        if (!key.StartsWith(StoredFile.TenantsPrefix, StringComparison.Ordinal))
            return null;
        var rest = key.AsSpan(StoredFile.TenantsPrefix.Length);
        var end = rest.IndexOf('/');
        var tenantId = end > 0 ? rest[..end].ToString() : null;
        return ObjectDeletes.IsTenantId(tenantId) ? tenantId : null;
    }
}
