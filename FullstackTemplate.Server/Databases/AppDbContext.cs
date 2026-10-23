namespace FullstackTemplate.Server.Databases;

using Domain;
using Domain.Tenants;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Services;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider,
    ICurrentUserService currentUserService,
    ITenantScopeProvider tenantScopeProvider,
    DomainEventDispatcher domainEventDispatcher) : DbContext(options)
{
    private const int MaxDomainEventRounds = 10;

    #region DbSet Region
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    #endregion

    private TenantScope? _tenantScope;
    private TenantScope? _domainEventTenantScope;
    private SaveStage _saveStage = SaveStage.Idle;

    /// <summary>
    /// The tenants whose rows the current caller can see. The tenant query filter reads it for each query.
    /// The context gets the scope once and keeps it, because the caller of a DI scope does not change.
    /// While the handlers of a domain event run, it is the tenant of the event.
    /// </summary>
    /// <remarks>
    /// The context is not pooled. If you add DbContext pooling, clear this value when the pool resets the context.
    /// </remarks>
    public TenantScope CurrentTenantScope =>
        _domainEventTenantScope ?? (_tenantScope ??= tenantScopeProvider.GetCurrentScope());

    internal bool IsRunningDomainEventHandlers => _saveStage == SaveStage.Dispatching;

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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.AddInterceptors(HandlerWriteGuard.Instance);
    }

    /// <summary>
    /// Refuses the save. The save runs async work, so <see cref="AppDbContext"/> has no sync save.
    /// <see cref="DbContext.SaveChanges()"/> also calls this overload.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => throw new NotSupportedException("AppDbContext has no sync save. Call SaveChangesAsync.");

    /// <summary>
    /// Runs the handlers of the queued domain events, then saves the changes and the handler changes in one
    /// transaction. <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> also calls this overload.
    /// A failed save ends the context, so a retry must use a new DI scope.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RefuseSave();
        try
        {
            var result = await SaveInStages(acceptAllChangesOnSuccess, cancellationToken);
            _saveStage = SaveStage.Idle;
            return result;
        }
        catch
        {
            _saveStage = SaveStage.Broken;
            throw;
        }
    }

    private async Task<int> SaveInStages(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        _saveStage = SaveStage.Dispatching;
        await RunDomainEventHandlers(cancellationToken);

        _saveStage = SaveStage.Saving;
        UpdateAuditFields();
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseConflict.From(exception) is { } conflict)
        {
            throw conflict;
        }
    }

    private void RefuseSave()
    {
        switch (_saveStage)
        {
            case SaveStage.Broken:
                throw new InvalidOperationException(
                    "An earlier save on this AppDbContext failed, so it cannot save again. Use a new DI scope.");
            case SaveStage.Dispatching or SaveStage.Saving:
                throw new InvalidOperationException(
                    "A domain event handler must not call SaveChangesAsync. The save that runs the handler saves its changes.");
        }

        if (Database.CurrentTransaction is not null)
            throw new NotSupportedException(
                "AppDbContext does not save inside a transaction that you opened. One save is one transaction.");
    }

    private async Task RunDomainEventHandlers(CancellationToken cancellationToken)
    {
        for (var round = 1; ; round++)
        {
            var queued = ChangeTracker.Entries<BaseEntity>()
                .SelectMany(entry => entry.Entity.TakeDomainEvents().Select(queuedEvent => (entry, queuedEvent)))
                .OrderBy(item => item.queuedEvent.Sequence)
                .ToList();
            if (queued.Count == 0)
                return;

            if (round > MaxDomainEventRounds)
            {
                var eventTypes = string.Join(", ", queued.Select(item => item.queuedEvent.Event.GetType().Name).Distinct());
                throw new InvalidOperationException(
                    $"The domain event handlers queued new events for {MaxDomainEventRounds} rounds, so they form a cycle. " +
                    $"The events of the next round: {eventTypes}.");
            }

            foreach (var (entry, queuedEvent) in queued)
            {
                _domainEventTenantScope = TenantQueryFilterExtension.TenantIdOfDomainEvent(entry) is { } tenantId
                    ? TenantScope.ForTenant(tenantId)
                    : null;
                try
                {
                    await domainEventDispatcher.DispatchAsync(queuedEvent.Event, this, cancellationToken);
                }
                finally
                {
                    _domainEventTenantScope = null;
                }
            }
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

    private enum SaveStage
    {
        Idle,
        Dispatching,
        Saving,
        Broken,
    }
}
