namespace FullstackTemplate.Server.Databases;

using System.Diagnostics;
using Auditing;
using Domain;
using Domain.Agents;
using Domain.AuditEntries;
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
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentPermission> AgentPermissions => Set<AgentPermission>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    #endregion

    private TenantScope? _tenantScope;
    private TenantScope? _domainEventTenantScope;
    private SaveStage _saveStage = SaveStage.Idle;
    private string? _requestName;

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

    /// <summary>
    /// Names the MediatR request whose saves this context writes, until the result is disposed. The outermost request
    /// wins: a nested Send shares this context and its saves, so it keeps the outer name.
    /// </summary>
    internal IDisposable EnterRequest(string requestName)
    {
        var outerRequestName = _requestName;
        _requestName ??= requestName;
        return new RequestNameRestorer(this, outerRequestName);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Automatically discovers and applies all IEntityTypeConfiguration<T> implementations in the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.MapEntityIdToField();
        modelBuilder.MapVersionToXmin();
        modelBuilder.FilterSoftDeletedRecords();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.FilterByTenant(this);
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
        var auditEntryCount = PrepareWrite();
        try
        {
            // The audit entries are in the tracker before the execution strategy runs, so a retry sends the same rows.
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken) - auditEntryCount;
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

    /// <summary>
    /// The steps before the write. It touches the owners of changed children, stamps the entities, turns deletes into
    /// soft deletes, and adds the audit entries of the changes to the tracker. Returns the number of audit entries.
    /// </summary>
    private int PrepareWrite()
    {
        // Entities that code removed from a collection become Deleted here.
        ChangeTracker.DetectChanges();
        RefuseAuditEntriesNotFromCapture();

        var auditModel = AuditModel.Of(Model);
        var aggregates = TrackedAggregates.Index(ChangeTracker, auditModel);
        TouchOwnersOfChangedChildren(aggregates);

        var now = timeProvider.GetUtcNow();
        StampEntities(now);

        var save = SaveFacts.From(now, currentUserService.UserIdentifier, currentUserService.ClientId, _requestName,
            Activity.Current?.TraceId.ToHexString());
        var auditEntries = AuditDiff.Compose(save, AuditDiff.Changes(AuditSnapshots.Take(aggregates, auditModel)));
        Set<AuditEntry>().AddRange(auditEntries);
        return auditEntries.Count;
    }

    private void RefuseAuditEntriesNotFromCapture()
    {
        if (ChangeTracker.Entries<AuditEntry>().Any(e => e.State != EntityState.Unchanged))
            throw new InvalidOperationException(
                "Only the save writes audit entries. Do not add, change, or remove an AuditEntry.");
    }

    // A changed child marks its Unchanged owner modified, so the owner row is written and its xmin changes (ADR 0008).
    private static void TouchOwnersOfChangedChildren(TrackedAggregates aggregates)
    {
        var changed = new Queue<EntityEntry<BaseEntity>>(aggregates.Changed);
        while (changed.TryDequeue(out var child))
        {
            if (aggregates.OwnerOf(child) is not { State: EntityState.Unchanged } owner)
                continue;

            owner.Property(e => e.LastModifiedOn).IsModified = true;
            changed.Enqueue(owner);
        }
    }

    private void StampEntities(DateTimeOffset now)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.UpdateCreationProperties(now, currentUserService.UserIdentifier, currentUserService.ClientId);
                    entry.Entity.UpdateModifiedProperties(now, currentUserService.UserIdentifier, currentUserService.ClientId);
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdateModifiedProperties(now, currentUserService.UserIdentifier, currentUserService.ClientId);
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.UpdateModifiedProperties(now, currentUserService.UserIdentifier, currentUserService.ClientId);
                    entry.Entity.UpdateIsDeleted(true);
                    break;
            }
        }
    }

    private sealed class RequestNameRestorer(AppDbContext context, string? outerRequestName) : IDisposable
    {
        public void Dispose() => context._requestName = outerRequestName;
    }

    private enum SaveStage
    {
        Idle,
        Dispatching,
        Saving,
        Broken,
    }
}
