namespace FullstackTemplate.Server.Domain.Users;

using System.Diagnostics;
using Databases;
using Exceptions;
using Microsoft.EntityFrameworkCore;

public static class ManagerCaseLoader
{
    /// <summary>
    /// Loads the managers of the tenant of the user and runs <see cref="TenantManagers.Decide"/>. Call it in the
    /// exclusive <see cref="RootLock.Tenant"/> of the tenant, and save in the same work.
    /// </summary>
    /// <exception cref="ValidationException">The change leaves the tenant with no user who can manage it.</exception>
    public static async Task<ManagerKept> KeepManagerAsync(this AppDbContext dbContext, User user, ManagerChange change,
        CancellationToken cancellationToken)
        => TenantManagers.Decide(await dbContext.LoadManagerCaseAsync(user, change, cancellationToken)) switch
        {
            ManagerDecision.Kept kept => kept.Proof,
            ManagerDecision.Refused { Refusal: ManagerRefusal.LastManager lastManager } => throw Refuse(lastManager),
            _ => throw new UnreachableException(),
        };

    private static async Task<ManagerCase> LoadManagerCaseAsync(this AppDbContext dbContext, User user,
        ManagerChange change, CancellationToken cancellationToken)
    {
        var needs = TenantManagers.Needs.Select(p => p.Value).ToList();
        var managerIds = await dbContext.Users
            .TagWithOperationCallSite("users.load_managers")
            .Where(u => u.TenantId == user.TenantId
                && u.UserPermissions.Select(p => p.Permission.Value).Where(v => needs.Contains(v)).Distinct().Count()
                    == needs.Count)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        // A permission add takes no lock, so it can land between the load of the user and this query.
        if (user.CanManageTenant != managerIds.Contains(user.Id))
        {
            Activity.Current?.SetTag("app.user.manager_refusal", "stale_version");
            throw new StaleVersionException();
        }

        return new ManagerCase(user, change, managerIds);
    }

    private static ValidationException Refuse(ManagerRefusal refusal)
    {
        Activity.Current?.SetTag("app.user.manager_refusal", "last_manager");
        return new ValidationException(refusal.Field, refusal.Message);
    }
}
