namespace FullstackTemplate.Server.Databases;

using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Auditing;
using Domain;
using Domain.Agents;
using Domain.AuditEntries;
using Domain.Tenants;
using Domain.Users;
using Domain.Invites;
using Domain.ObjectDeletes;
// new-feature:db-usings
using EntityConfigurations;
// example:start
using Examples.Venues;
using Examples.Workshops;
// example:end
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Resources;
using Resources.Jobs;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider,
    ICurrentUserService currentUserService,
    ICallerIdentityResolver callerIdentityResolver,
    ITenantScopeProvider tenantScopeProvider,
    DomainEventDispatcher domainEventDispatcher,
    IJobWriter jobWriter) : DbContext(options)
{
    private const int MaxDomainEventRounds = 10;

    #region DbSet Region
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentPermission> AgentPermissions => Set<AgentPermission>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    // example:start
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<WorkshopAttachment> WorkshopAttachments => Set<WorkshopAttachment>();
    // example:end
    public DbSet<Invite> Invites => Set<Invite>();
    // new-feature:db-sets
    #endregion

    private TenantScope? _tenantScope;
    private TenantScope? _narrowedTenantScope;
    private SaveStage _saveStage = SaveStage.Idle;
    private string? _requestName;
    private readonly List<AuditAction> _recordedActions = [];
    private readonly List<PendingJob> _queuedJobs = [];
    private bool _jobsArmed;
    private Guid? _firstWrittenJob;

    /// <summary>
    /// The tenants whose rows the current caller can see. The tenant query filter reads it for each query.
    /// The context gets the scope once and keeps it, because the caller of a DI scope does not change.
    /// While the handlers of a domain event run, it is the tenant of the event. In <see cref="InTenantScopeAsync{T}"/>,
    /// it is the tenant of that call.
    /// </summary>
    /// <remarks>
    /// The context is not pooled. If you add DbContext pooling, clear this value when the pool resets the context.
    /// </remarks>
    public TenantScope CurrentTenantScope =>
        _narrowedTenantScope ?? (_tenantScope ??= tenantScopeProvider.GetCurrentScope());

    internal bool IsRunningDomainEventHandlers => _saveStage == SaveStage.Dispatching;

    /// <summary>
    /// Makes the next query get the tenant scope again. Call it only when code changes the caller of a live
    /// DI scope, for example a test that changes the current user after a query.
    /// </summary>
    public void RefreshTenantScope() => _tenantScope = null;

    internal async Task<T> InTenantScopeAsync<T>(string tenantId, Func<Task<T>> work)
    {
        var scope = CurrentTenantScope;
        if (!scope.IncludesAllTenants && scope.TenantId != tenantId)
            throw new InvalidOperationException($"The caller cannot see tenant {tenantId}, so its scope cannot become the scope of that tenant.");

        var outer = _narrowedTenantScope;
        _narrowedTenantScope = TenantScope.ForTenant(tenantId);
        try
        {
            return await work();
        }
        finally
        {
            _narrowedTenantScope = outer;
        }
    }

    /// <summary>
    /// Names the MediatR request whose saves this context writes, until the result is disposed. The outermost request
    /// wins: a nested Send shares this context and its saves, so it keeps the outer name.
    /// </summary>
    internal EnteredRequest EnterRequest(string requestName)
    {
        var entered = new EnteredRequest(this, _requestName);
        _requestName ??= requestName;
        return entered;
    }

    /// <summary>
    /// Adds the action to the audit log of the next save, in the same transaction and the same record as the changes of
    /// that save. Save also when the handler changes no data. <see cref="Resources.AuditRequestBehavior{TRequest,TResponse}"/>
    /// throws when a request records an action and does not save it.
    /// </summary>
    public void RecordAction(AuditAction action) => _recordedActions.Add(action);

    internal bool HasUnsavedActions => _recordedActions.Count > 0;

    /// <summary>
    /// Queues <paramref name="job"/> to commit with the next save. BackWave runs it after the commit. If the save fails,
    /// the job does not exist. Queue it in a handler or a domain event handler, then save, also when the handler changes
    /// no data. <see cref="Resources.AuditRequestBehavior{TRequest,TResponse}"/> throws when a request queues a job and
    /// does not save it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The save is writing, an earlier save failed, or the caller cannot see the tenant of the job.
    /// </exception>
    /// <exception cref="ArgumentException">The tenant ID of the job is not a tenant ID.</exception>
    public void QueueJob<TJob>(TJob job,
        [CallerFilePath] string callerFilePath = "",
        [CallerMemberName] string callerMemberName = "",
        [CallerLineNumber] int callerLineNumber = 0)
        where TJob : class, ITenantJob
    {
        switch (_saveStage)
        {
            case SaveStage.Broken:
                throw new InvalidOperationException(
                    "An earlier save on this AppDbContext failed, so it cannot queue a job. Use a new DI scope.");
            case SaveStage.Saving:
                throw new InvalidOperationException(
                    "Queue a job before the save writes: in a handler, or in a domain event handler.");
        }

        if (!EntityId.IsValid(job.TenantId, Tenant.IdPrefix))
            throw new ArgumentException($"{typeof(TJob).Name} has the tenant ID '{job.TenantId}', which is not a tenant ID.",
                nameof(job));
        // In a domain event handler, the scope is the tenant of the event.
        if (!CurrentTenantScope.Includes(job.TenantId))
            throw new InvalidOperationException(
                $"The caller cannot see tenant {job.TenantId}, so it cannot queue {typeof(TJob).Name} for that tenant.");

        _queuedJobs.Add(PendingJob.Of(job, callerFilePath, callerMemberName, callerLineNumber));
    }

    internal bool HasUnsavedJobs => _queuedJobs.Count > 0;

    /// <summary>
    /// <see cref="JobCommitHook"/> calls it just before EF commits a transaction of this context. Only the transaction of
    /// a save with queued jobs writes them.
    /// </summary>
    internal async Task WriteQueuedJobsAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
        if (_jobsArmed)
            _firstWrittenJob = await jobWriter.WriteAsync(_queuedJobs, transaction, cancellationToken);
    }

    /// <summary>True while <see cref="RootLock"/> runs its work, so a save looks for its commit after a lost reply.</summary>
    internal bool InLockedWork { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Automatically discovers and applies all IEntityTypeConfiguration<T> implementations in the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.MapEntityIdToField();
        configurationBuilder.MapVersionToXmin();
        configurationBuilder.FilterSoftDeletedRecords();
        configurationBuilder.FilterByTenant(this);
        configurationBuilder.MapConstraintErrors();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.AddInterceptors(WriteGuard.Instance, JobCommitHook.Instance);
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
    /// A failed save ends the context, so a retry must use a new DI scope. Only
    /// <see cref="Resources.RaceRetryBehavior{TRequest,TResponse}"/> runs a command again in the same scope.
    /// </summary>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RefuseSave();
        return EndOnFailure(SaveInStages(acceptAllChangesOnSuccess, cancellationToken));
    }

    /// <summary>
    /// Inserts new aggregates with Postgres COPY, for an import of many rows. It is a save with another writer: the
    /// same stamps, a Created audit entry for each row, the recorded actions, and the SaveChanges interceptors, in one
    /// transaction. After it, the entities are tracked and Unchanged with their <c>Version</c>, as after a save. A
    /// failed bulk insert ends the context, as a failed save does.
    /// </summary>
    /// <returns>The number of entity rows that it wrote, the children of the roots too.</returns>
    /// <exception cref="InvalidOperationException">
    /// The context has pending changes, <typeparamref name="TRoot"/> is a child of an owner, or an entity queued a
    /// domain event that has a handler. Each refusal of <see cref="SaveChangesAsync(bool, CancellationToken)"/> applies
    /// too.
    /// </exception>
    public Task<int> BulkInsertAsync<TRoot>(IEnumerable<TRoot> roots, CancellationToken cancellationToken = default)
        where TRoot : BaseEntity
    {
        RefuseSave();
        RefuseBulkInsert(Set<TRoot>().EntityType);
        return EndOnFailure(BulkInsertInStages(roots, cancellationToken));
    }

    // The tracker of a failed write can hold the stamps and the audit entries of a write that did not commit.
    private async Task<int> EndOnFailure(Task<int> write)
    {
        try
        {
            var result = await write;
            _saveStage = SaveStage.Idle;
            return result;
        }
        catch
        {
            _saveStage = SaveStage.Broken;
            throw;
        }
        finally
        {
            // The save wrote the queued jobs, or it failed, and a failed save ends the context.
            _queuedJobs.Clear();
        }
    }

    /// <summary>
    /// Forgets the work of a command that lost a race, so <see cref="Resources.RaceRetryBehavior{TRequest,TResponse}"/>
    /// can run it again in the same DI scope. The lost save rolled back, so the database has none of its changes. It
    /// clears the tracked entities and the recorded audit actions, and it ends the refusal of a save after a failure.
    /// <see cref="RootLock"/> also calls it before it runs its work again after a transient failure.
    /// </summary>
    internal void DiscardLostRace()
    {
        if (_saveStage is SaveStage.Dispatching or SaveStage.Saving)
            throw new InvalidOperationException("A lost race can be discarded only after its save ends.");

        ChangeTracker.Clear();
        _recordedActions.Clear();
        _queuedJobs.Clear();
        _saveStage = SaveStage.Idle;
    }

    private async Task<int> SaveInStages(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        _saveStage = SaveStage.Dispatching;
        await RunDomainEventHandlers(cancellationToken);

        _saveStage = SaveStage.Saving;
        var auditEntries = PrepareWrite(await callerIdentityResolver.GetAsync(cancellationToken));
        var transactionBehavior = Database.AutoTransactionBehavior;
        try
        {
            if (auditEntries.Count == 0 && _queuedJobs.Count == 0)
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            var written = ChangeTracker.Entries()
                .Where(e => e.State is not (EntityState.Unchanged or EntityState.Detached))
                .ToList();

            // After a lost reply of the commit, the audit entries tell whether it committed. EF dropped the Version
            // that the failed try read back. Each row that one transaction writes has the same xmin, so the xmin of an
            // entry gives it. The queued jobs are in the same transaction, so this check covers them too.
            async Task<ExecutionResult<int>> AuditEntriesCommittedAsync(bool accept, CancellationToken ct)
            {
                var (occurredAt, recordId) = (auditEntries[0].OccurredAt, auditEntries[0].RecordId);
                var version = await AuditEntries
                    .TagWithOperationCallSite("audit_entries.save_committed")
                    .IgnoreQueryFilters([QueryFilterNames.Tenant])
                    .Where(e => e.OccurredAt == occurredAt && e.RecordId == recordId)
                    .Select(e => (uint?)EF.Property<uint>(e, AuditEntryConfiguration.TransactionId))
                    .FirstOrDefaultAsync(ct);
                if (version is null)
                    return new ExecutionResult<int>(false, 0);

                Telemetry.AddEvent([], "database.save.lost_reply_committed");
                foreach (var entry in written.Where(e => e.Entity is BaseEntity))
                    entry.Property(nameof(BaseEntity.Version)).CurrentValue = version.Value;
                if (accept)
                    ChangeTracker.AcceptAllChanges();
                return new ExecutionResult<int>(true, written.Count);
            }

            // A save with jobs and no audit entry looks for its first job. No audit entry gives the xmin of the commit, so
            // a BaseEntity that the save wrote with no recorded change reads its Version again.
            async Task<ExecutionResult<int>> JobsCommittedAsync(bool accept, CancellationToken ct)
            {
                if (_firstWrittenJob is not { } jobId || !await jobWriter.ExistsAsync(jobId, ct))
                    return new ExecutionResult<int>(false, 0);

                Telemetry.AddEvent([], "database.save.lost_reply_committed");
                foreach (var entry in written.Where(e => e.Entity is BaseEntity))
                {
                    if (await entry.GetDatabaseValuesAsync(ct) is { } stored)
                        entry.Property(nameof(BaseEntity.Version)).CurrentValue = stored[nameof(BaseEntity.Version)];
                }
                if (accept)
                    ChangeTracker.AcceptAllChanges();
                return new ExecutionResult<int>(true, written.Count);
            }

            Func<bool, CancellationToken, Task<int>> write = written.Count > 0 ? base.SaveChangesAsync : CommitJobsOnlyAsync;
            Func<bool, CancellationToken, Task<ExecutionResult<int>>> findCommit =
                auditEntries.Count > 0 ? AuditEntriesCommittedAsync : JobsCommittedAsync;

            // EF runs a nested strategy one time and skips its check, so in the work of a RootLock the save looks for
            // the commit itself. Else the lock runs the whole work again, and it finds the saved rows.
            async Task<int> SaveInLockAsync(bool accept, CancellationToken ct)
            {
                try
                {
                    return await write(accept, ct);
                }
                // The same test as the Npgsql retrying strategy, whose detector is internal.
                catch (Exception failure) when (ExecutionStrategy.CallOnWrappedException(
                    failure, e => e is NpgsqlException { IsTransient: true } or TimeoutException))
                {
                    ExecutionResult<int>? commit = null;
                    try
                    {
                        commit = await findCommit(accept, ct);
                    }
                    catch (Exception)
                    {
                        // The lost reply is the failure to report. The lock then runs the work again.
                    }

                    if (commit is not { IsSuccessful: true })
                        throw;
                    return commit.Result;
                }
            }

            if (_queuedJobs.Count > 0)
            {
                // EF opens no transaction for a save of one statement, and with no transaction JobCommitHook does not run.
                Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
                _jobsArmed = true;
            }

            // The audit entries are in the tracker before the execution strategy runs, so a retry sends the same rows.
            // A retry writes the queued jobs again with new IDs. The jobs of a try that rolled back do not exist.
            return await Database.CreateExecutionStrategy().ExecuteAsync(
                acceptAllChangesOnSuccess,
                (_, accept, ct) => InLockedWork ? SaveInLockAsync(accept, ct) : write(accept, ct),
                (_, accept, ct) => findCommit(accept, ct),
                cancellationToken) - auditEntries.Count;
        }
        catch (DbUpdateException exception)
        {
            // Not in a filter, because a filter loses an exception that it throws.
            if (DatabaseConflict.From(exception, Model) is { } conflict)
                throw conflict;
            throw;
        }
        finally
        {
            _jobsArmed = false;
            _firstWrittenJob = null;
            Database.AutoTransactionBehavior = transactionBehavior;
        }
    }

    // A save of jobs with no row. It commits an empty transaction, so that JobCommitHook writes the jobs.
    private async Task<int> CommitJobsOnlyAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return 0;
    }

    private async Task<int> BulkInsertInStages<TRoot>(IEnumerable<TRoot> roots, CancellationToken cancellationToken)
        where TRoot : BaseEntity
    {
        _saveStage = SaveStage.Saving;
        Set<TRoot>().AddRange(roots);
        TakeDomainEventsWithNoHandler();
        var auditEntries = PrepareWrite(await callerIdentityResolver.GetAsync(cancellationToken));
        if (auditEntries.Count == 0)
            return 0;

        var entities = ChangeTracker.Entries<BaseEntity>().Where(e => e.State == EntityState.Added).ToList();
        using var activity = Telemetry.Trace(
            [("app.bulk_insert.entity_type", typeof(TRoot).Name), ("app.bulk_insert.row_count", entities.Count)],
            "database.bulk_insert");

        // The owners before their children, so that each foreign key finds its row.
        var auditModel = AuditModel.Of(Model);
        int OwnerDepth(IEntityType type)
            => auditModel.OwnerKeyOf(type) is { } ownerKey ? 1 + OwnerDepth(ownerKey.PrincipalEntityType) : 0;
        var tables = entities.GroupBy(e => e.Metadata).OrderBy(table => OwnerDepth(table.Key))
            .Concat<IGrouping<IEntityType, EntityEntry>>(ChangeTracker.Entries<AuditEntry>()
                .Where(e => e.State == EntityState.Added).GroupBy(e => e.Metadata))
            .ToList();
        var (occurredAt, recordId) = (auditEntries[0].OccurredAt, auditEntries[0].RecordId);

        // The SaveChanges interceptors see the bulk insert as a save, so TenantLookupInterceptor forgets a cached miss
        // of each new user.
        var updateLogger = this.GetService<IDiagnosticsLogger<DbLoggerCategory.Update>>();
        await updateLogger.SaveChangesStartingAsync(this, cancellationToken);
        try
        {
            var version = await Database.CreateExecutionStrategy().ExecuteInTransactionAsync(
                ct => PostgresCopy.WriteAsync(Database, tables, ct),
                // After a lost acknowledgment of the commit, the audit entries tell whether it committed.
                verifySucceeded: ct => AuditEntries
                    .TagWithOperationCallSite("audit_entries.bulk_insert_committed")
                    .IgnoreQueryFilters([QueryFilterNames.Tenant])
                    .AnyAsync(e => e.OccurredAt == occurredAt && e.RecordId == recordId, ct),
                cancellationToken);

            foreach (var entry in entities)
                entry.Property(e => e.Version).CurrentValue = version;
            ChangeTracker.AcceptAllChanges();
            return await updateLogger.SaveChangesCompletedAsync(this, entities.Count, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await updateLogger.SaveChangesCanceledAsync(this, cancellationToken);
            throw;
        }
        catch (Exception exception)
        {
            await updateLogger.SaveChangesFailedAsync(this, exception, cancellationToken);
            var conflict = exception is PostgresException postgres ? DatabaseConflict.From(postgres, Model) : null;
            activity?.SetTag("error.type", (conflict ?? exception).GetType().FullName);
            if (conflict is not null)
                throw conflict;

            Telemetry.MarkFailed(activity, exception);
            throw;
        }
    }

    private void RefuseBulkInsert(IEntityType rootType)
    {
        if (AuditModel.Of(Model).OwnerKeyOf(rootType) is { } ownerKey)
        {
            var owner = ownerKey.PrincipalEntityType.ClrType.Name;
            throw new InvalidOperationException(
                $"BulkInsertAsync inserts aggregate roots, and a {rootType.ClrType.Name} is a part of its {owner}. Add it to its {owner}, then bulk insert the {owner}.");
        }

        if (ChangeTracker.HasChanges())
            throw new InvalidOperationException(
                "BulkInsertAsync writes only the entities that it inserts, but this AppDbContext has pending changes. Save them first.");
        if (_queuedJobs.Count > 0)
            throw new InvalidOperationException(
                "BulkInsertAsync does not write queued jobs. Save them with SaveChangesAsync first.");
    }

    // A save takes the queued events and runs their handlers. A bulk insert runs no handler, so it can take only the
    // events that have no handler, which the save drops too.
    private void TakeDomainEventsWithNoHandler()
    {
        var entities = ChangeTracker.Entries<BaseEntity>().Select(entry => entry.Entity).ToList();
        var handled = entities.SelectMany(entity => entity.DomainEvents)
            .Select(domainEvent => domainEvent.GetType())
            .Distinct()
            .Where(domainEventDispatcher.HasHandlers)
            .Select(eventType => eventType.Name)
            .ToList();
        if (handled.Count > 0)
            throw new InvalidOperationException(
                $"BulkInsertAsync runs no domain event handlers, but {string.Join(", ", handled)} has a handler. Save these entities with SaveChangesAsync.");

        foreach (var entity in entities)
            entity.TakeDomainEvents();
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

            var outer = _narrowedTenantScope;
            foreach (var (entry, queuedEvent) in queued)
            {
                _narrowedTenantScope = TenantQueryFilterExtension.TenantIdOfDomainEvent(entry) is { } tenantId
                    ? TenantScope.ForTenant(tenantId)
                    : outer;
                try
                {
                    await domainEventDispatcher.DispatchAsync(queuedEvent.Event, this, cancellationToken);
                }
                finally
                {
                    _narrowedTenantScope = outer;
                }
            }
        }
    }

    /// <summary>
    /// The steps before the write of a save and of a bulk insert. It touches the owners of changed children, queues the
    /// object deletes of the deleted rows, stamps the entities, turns deletes into soft deletes, and adds the audit
    /// entries of the changes and the recorded actions to the tracker. Returns the audit entries.
    /// </summary>
    private IReadOnlyList<AuditEntry> PrepareWrite(CallerIdentity caller)
    {
        // Entities that code removed from a collection become Deleted here.
        ChangeTracker.DetectChanges();
        RefuseAuditEntriesNotFromCapture();

        var auditModel = AuditModel.Of(Model);
        var aggregates = TrackedAggregates.Index(ChangeTracker, auditModel);
        aggregates.GuardRootsOf(_recordedActions);
        TouchOwnersOfChangedChildren(aggregates);

        // Before the stamp turns the deletes into soft deletes
        _queuedJobs.AddRange(ObjectDeletes.ForDeleted(
            ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted).Select(e => e.Entity)));
        var now = timeProvider.GetUtcNow();
        StampEntities(now);

        var actor = AuditActor.Of(currentUserService.UserIdentifier, currentUserService.ClientId, caller);
        var save = SaveFacts.From(now, actor, _requestName, Activity.Current?.TraceId.ToHexString());
        var auditEntries = AuditDiff.Compose(save, AuditDiff.Changes(AuditSnapshots.Take(aggregates, auditModel)),
            _recordedActions);
        _recordedActions.Clear();
        Set<AuditEntry>().AddRange(auditEntries);
        return auditEntries;
    }

    private void RefuseAuditEntriesNotFromCapture()
    {
        if (ChangeTracker.Entries<AuditEntry>().Any(e => e.State != EntityState.Unchanged))
            throw new InvalidOperationException(
                "Only the save writes audit entries. Do not add, change, or remove an AuditEntry.");
    }

    // A changed child marks its Unchanged owner modified, so the owner row is written and its xmin changes (ADR 0008).
    // A child that moves to another owner, or to no owner, also changes the aggregate of the owner that it leaves.
    private static void TouchOwnersOfChangedChildren(TrackedAggregates aggregates)
    {
        var changed = new Queue<EntityEntry<BaseEntity>>(aggregates.Changed);
        while (changed.TryDequeue(out var child))
        {
            foreach (var owner in new[] { aggregates.OwnerOf(child), aggregates.FormerOwnerOf(child) })
            {
                if (owner is not { State: EntityState.Unchanged })
                    continue;

                owner.Property(e => e.LastModifiedOn).IsModified = true;
                changed.Enqueue(owner);
            }
        }
    }

    private void StampEntities(DateTimeOffset now)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added or EntityState.Modified:
                    entry.Entity.UpdateLastModifiedOn(now);
                    break;

                case EntityState.Deleted:
                    if (entry.Entity is IDeletedWithEvent { DeleteQueued: false })
                        throw new InvalidOperationException(entry.Entity is User
                            ? "Remove a user with Remove(user.Delete()), so that the save queues UserDeleted."
                            : $"Remove a {entry.Metadata.ClrType.Name} with Remove(entity.Delete()), so that the save queues its delete event.");

                    entry.State = EntityState.Modified;
                    entry.Entity.UpdateLastModifiedOn(now);
                    entry.Entity.UpdateIsDeleted(true);
                    break;
            }
        }
    }

    /// <summary>Gives the context back the name of the outer request when disposed.</summary>
    internal sealed class EnteredRequest(AppDbContext context, string? outerRequestName) : IDisposable
    {
        public bool IsOutermost => outerRequestName is null;

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
