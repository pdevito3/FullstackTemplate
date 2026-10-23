namespace FullstackTemplate.Server.Databases;

using Domain;
using Domain.Tenants;
using Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Services;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider,
    ICurrentUserService currentUserService,
    ITenantScopeProvider tenantScopeProvider,
    IMediator mediator) : DbContext(options)
{
    #region DbSet Region
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    #endregion

    private TenantScope? _tenantScope;

    /// <summary>
    /// The tenants whose rows the current caller can see. The tenant query filter reads it for each query.
    /// The context gets the scope once and keeps it, because the caller of a DI scope does not change.
    /// </summary>
    /// <remarks>
    /// The context is not pooled. If you add DbContext pooling, clear this value when the pool resets the context.
    /// </remarks>
    public TenantScope CurrentTenantScope => _tenantScope ??= tenantScopeProvider.GetCurrentScope();

    /// <summary>
    /// Makes the next query get the tenant scope again. Call it only when code changes the caller of a live
    /// DI scope, for example a test that changes the current user after a query.
    /// </summary>
    public void RefreshTenantScope() => _tenantScope = null;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Automatically discovers and applies all IEntityTypeConfiguration<T> implementations in the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.MapEntityIdToField();
        modelBuilder.MapVersionToXmin();
        modelBuilder.FilterSoftDeletedRecords();
        modelBuilder.FilterByTenant(this);
    }

    /// <summary>
    /// Refuses the save. The save runs async work, so <see cref="AppDbContext"/> has no sync save.
    /// <see cref="DbContext.SaveChanges()"/> also calls this overload.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => throw new NotSupportedException("AppDbContext has no sync save. Call SaveChangesAsync.");

    /// <summary>
    /// Saves the changes. <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> also calls this overload.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        int result;
        try
        {
            result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseConflict.From(exception) is { } conflict)
        {
            throw conflict;
        }
        await DispatchDomainEvents();
        return result;
    }

    private async Task DispatchDomainEvents()
    {
        var domainEventEntities = ChangeTracker.Entries<BaseEntity>()
            .Select(po => po.Entity)
            .Where(po => po.DomainEvents.Any())
            .ToArray();

        foreach (var entity in domainEventEntities)
        {
            var events = entity.DomainEvents.ToArray();
            entity.ClearDomainEvents();
            foreach (var domainEvent in events)
                await mediator.Publish(domainEvent);
        }
    }

    private void UpdateAuditFields()
    {
        GuardOwnerVersionsOfChangedChildren();

        var now = timeProvider.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.UpdateCreationProperties(now, currentUserService.UserIdentifier);
                    entry.Entity.UpdateModifiedProperties(now, currentUserService.UserIdentifier);
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdateModifiedProperties(now, currentUserService.UserIdentifier);
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.UpdateModifiedProperties(now, currentUserService.UserIdentifier);
                    entry.Entity.UpdateIsDeleted(true);
                    break;
            }
        }
    }

    private void GuardOwnerVersionsOfChangedChildren()
    {
        var entries = ChangeTracker.Entries<BaseEntity>().ToList();
        var changed = new Queue<EntityEntry<BaseEntity>>(entries.Where(e =>
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted));

        while (changed.TryDequeue(out var child))
        {
            foreach (var ownership in child.Metadata.GetForeignKeys().Where(fk =>
                         fk.DeleteBehavior == DeleteBehavior.Cascade
                         && fk.PrincipalEntityType.ClrType.IsAssignableTo(typeof(BaseEntity))))
            {
                var ownerIdProperty = child.Property(ownership.Properties.Single().Name);
                var ownerId = child.State == EntityState.Deleted
                    ? ownerIdProperty.OriginalValue
                    : ownerIdProperty.CurrentValue;
                var owner = entries.SingleOrDefault(e =>
                                e.Metadata == ownership.PrincipalEntityType && Equals(e.Entity.Id, ownerId))
                            ?? throw new InvalidOperationException(
                                $"A {child.Metadata.ClrType.Name} changed, but its {ownership.PrincipalEntityType.ClrType.Name} '{ownerId}' is not tracked. Load the {ownership.PrincipalEntityType.ClrType.Name}, then change the {child.Metadata.ClrType.Name} through it.");
                if (owner.State != EntityState.Unchanged)
                    continue;

                owner.Property(e => e.LastModifiedOn).IsModified = true;
                changed.Enqueue(owner);
            }
        }
    }
}
