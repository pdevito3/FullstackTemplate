namespace FullstackTemplate.Server.Databases.Auditing;

using Domain;
using Domain.AuditEntries;
using Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// The tracked entities indexed by type and id, with the ownership rule of <see cref="AuditModel.OwnerKeyOf"/>. The
/// owner touch and the audit capture both read it, so they cannot disagree about what an aggregate is.
/// </summary>
internal sealed class TrackedAggregates
{
    private readonly AuditModel _model;
    private readonly Dictionary<(IEntityType Type, string Id), EntityEntry<BaseEntity>> _entries;
    private readonly Dictionary<BaseEntity, EntityEntry<BaseEntity>?> _owners = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<BaseEntity, AuditSubject> _subjects = new(ReferenceEqualityComparer.Instance);

    private TrackedAggregates(AuditModel model, List<EntityEntry<BaseEntity>> entries)
    {
        _model = model;
        _entries = entries.ToDictionary(e => (e.Metadata.GetRootType(), e.Entity.Id));
        Changed = entries.Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
    }

    /// <summary>The entries that were Added, Modified, or Deleted when the index was made.</summary>
    public IReadOnlyList<EntityEntry<BaseEntity>> Changed { get; }

    /// <summary>
    /// Indexes the tracker and finds the subject of each changed entry now, while a deleted entry still shows the
    /// Deleted state.
    /// </summary>
    public static TrackedAggregates Index(ChangeTracker tracker, AuditModel model)
    {
        var aggregates = new TrackedAggregates(model, tracker.Entries<BaseEntity>().ToList());
        foreach (var entry in aggregates.Changed)
            aggregates._subjects[entry.Entity] = aggregates.SubjectOfEntry(entry);
        return aggregates;
    }

    public AuditSubject SubjectOf(EntityEntry<BaseEntity> changed) => _subjects[changed.Entity];

    /// <summary>Null for an aggregate root.</summary>
    /// <exception cref="InvalidOperationException">The owner is not tracked.</exception>
    public EntityEntry<BaseEntity>? OwnerOf(EntityEntry<BaseEntity> child)
    {
        if (_owners.TryGetValue(child.Entity, out var known))
            return known;

        EntityEntry<BaseEntity>? owner = null;
        if (_model.OwnerKeyOf(child.Metadata) is { } ownerKey)
        {
            var ownerIdProperty = child.Property(ownerKey.Properties.Single().Name);
            var ownerId = child.State == EntityState.Deleted ? ownerIdProperty.OriginalValue : ownerIdProperty.CurrentValue;
            var ownerType = ownerKey.PrincipalEntityType;
            owner = ownerId is string id && _entries.TryGetValue((ownerType.GetRootType(), id), out var tracked)
                ? tracked
                : throw new InvalidOperationException(
                    $"A {child.Metadata.ClrType.Name} changed, but its {ownerType.ClrType.Name} '{ownerId}' is not tracked. Load the {ownerType.ClrType.Name}, then change the {child.Metadata.ClrType.Name} through it.");
        }

        _owners[child.Entity] = owner;
        return owner;
    }

    private AuditSubject SubjectOfEntry(EntityEntry<BaseEntity> entry)
    {
        var root = entry;
        while (OwnerOf(root) is { } owner)
            root = owner;

        return new AuditSubject(entry.Metadata.ClrType.Name, entry.Entity.Id, root.Metadata.ClrType.Name,
            root.Entity.Id, TenantOf(root));
    }

    private static string TenantOf(EntityEntry<BaseEntity> root) => root.Entity switch
    {
        Tenant tenant => tenant.Id,
        ITenantable tenantable => tenantable.TenantId,
        _ => throw new InvalidOperationException(
            $"{root.Metadata.ClrType.Name} is an aggregate root with no tenant, so the audit log cannot find the tenant of its changes. Implement ITenantable on it, or give it an owner through a Cascade foreign key."),
    };
}

/// <summary>Reads the changed entries of the tracker into plain snapshots. It runs after the stamps.</summary>
internal static class AuditSnapshots
{
    public static IEnumerable<EntitySnapshot> Take(TrackedAggregates aggregates, AuditModel model)
    {
        foreach (var entry in aggregates.Changed)
        {
            var isAdded = entry.State == EntityState.Added;
            var isDeleted = entry.Property(e => e.IsDeleted);
            var properties = model.ValuesOf(entry.Metadata)
                .Where(value => value.IsRecorded)
                .Select(value => Snapshot(entry, value, isAdded))
                .ToList();

            yield return new EntitySnapshot(aggregates.SubjectOf(entry), isAdded,
                WasDeleted: !isAdded && isDeleted.OriginalValue, IsDeleted: isDeleted.CurrentValue, properties);
        }
    }

    // A snapshot holds provider values, so it holds what the column holds.
    private static PropertySnapshot Snapshot(EntityEntry<BaseEntity> entry, AuditedValue value, bool isAdded)
    {
        var property = value.Complex is null
            ? entry.Property(value.Property)
            : entry.ComplexProperty(value.Complex).Property(value.Property);
        var converter = value.Property.GetTypeMapping().Converter;

        object? ToProvider(object? model) => model is null || converter is null ? model : converter.ConvertToProvider(model);

        return new PropertySnapshot(value.Path, isAdded ? null : ToProvider(property.OriginalValue),
            ToProvider(property.CurrentValue));
    }
}
