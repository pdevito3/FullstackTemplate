namespace FullstackTemplate.Server.Databases;

using Domain;
using Microsoft.EntityFrameworkCore;
using Services;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider,
    ICurrentUserService currentUserService) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.FilterSoftDeletedRecords();
    }

    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateAuditFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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
