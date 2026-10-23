namespace FullstackTemplate.Server.Services;

using Databases;
using Domain.Tenants;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Resources;

public sealed record TenantLookupChange(IReadOnlySet<string> StaleIdentifiers, IReadOnlySet<string> ChangedTenantIds)
{
    public sealed record UserLookup(string Identifier, string TenantId, bool IsDeleted);

    public sealed record UserEntry(EntityState State, UserLookup Original, UserLookup Current);

    public sealed record TenantEntry(string TenantId, bool OriginalIsDeleted, bool CurrentIsDeleted);

    /// <summary>
    /// It compares the original and the current values, not <c>IsModified</c>, because the soft delete sets the state
    /// to Modified, which marks each property modified.
    /// </summary>
    public static TenantLookupChange Read(IEnumerable<UserEntry> users, IEnumerable<TenantEntry> tenants)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var user in users)
        {
            switch (user.State)
            {
                case EntityState.Added:
                    identifiers.Add(user.Current.Identifier);
                    break;
                case EntityState.Modified when user.Original != user.Current:
                    identifiers.Add(user.Original.Identifier);
                    identifiers.Add(user.Current.Identifier);
                    break;
            }
        }

        var tenantIds = tenants
            .Where(tenant => tenant.OriginalIsDeleted != tenant.CurrentIsDeleted)
            .Select(tenant => tenant.TenantId)
            .ToHashSet(StringComparer.Ordinal);

        return new TenantLookupChange(identifiers, tenantIds);
    }

    public static async Task<TenantLookupChange> CollectAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var change = Read(
            dbContext.ChangeTracker.Entries<User>().Select(ToUserEntry).ToList(),
            dbContext.ChangeTracker.Entries<Tenant>().Select(ToTenantEntry).ToList());
        if (change.ChangedTenantIds.Count == 0)
            return change;

        var changedTenantIds = change.ChangedTenantIds.ToList();
        var usersOfChangedTenants = await dbContext.Users
            .TagWithOperationCallSite("tenant_lookup.users_of_changed_tenants")
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(u => changedTenantIds.Contains(u.TenantId))
            .Select(u => u.Identifier)
            .ToListAsync(cancellationToken);

        return change with
        {
            StaleIdentifiers = change.StaleIdentifiers.Concat(usersOfChangedTenants).ToHashSet(StringComparer.Ordinal),
        };
    }

    public async Task ApplyAsync(ITenantIdProvider tenantIdProvider, CancellationToken cancellationToken)
    {
        if (StaleIdentifiers.Count == 0)
            return;

        foreach (var identifier in StaleIdentifiers)
            await tenantIdProvider.InvalidateCacheAsync(identifier, cancellationToken);

        Telemetry.AddEvent([("app.tenant_lookup.removed_count", StaleIdentifiers.Count)], "tenant_lookup.removed");
    }

    private static UserEntry ToUserEntry(EntityEntry<User> entry) => new(
        entry.State,
        new UserLookup(
            entry.Property(u => u.Identifier).OriginalValue,
            entry.Property(u => u.TenantId).OriginalValue,
            entry.Property(u => u.IsDeleted).OriginalValue),
        new UserLookup(entry.Entity.Identifier, entry.Entity.TenantId, entry.Entity.IsDeleted));

    private static TenantEntry ToTenantEntry(EntityEntry<Tenant> entry) => new(
        entry.Entity.Id,
        entry.Property(t => t.IsDeleted).OriginalValue,
        entry.Entity.IsDeleted);
}
