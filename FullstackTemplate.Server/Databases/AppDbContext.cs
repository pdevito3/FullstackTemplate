namespace FullstackTemplate.Server.Databases;

using Domain;
using Domain.Tenants;
using Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
        modelBuilder.FilterSoftDeletedRecords();
        modelBuilder.FilterByTenant(this);
    }

    public override int SaveChanges()
    {
        UpdateAuditFields();
        var result = base.SaveChanges();
        DispatchDomainEvents().GetAwaiter().GetResult();
        return result;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateAuditFields();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        DispatchDomainEvents().GetAwaiter().GetResult();
        return result;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        var result = await base.SaveChangesAsync(cancellationToken);
        await DispatchDomainEvents();
        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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
}
