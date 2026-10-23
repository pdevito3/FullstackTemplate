namespace FullstackTemplate.Server.Domain.Users;

using System.Diagnostics;
using Databases;
using Exceptions;
using Microsoft.EntityFrameworkCore;
using Services;

/// <summary>
/// The one way for a handler to require a permission of the current caller in the current tenant.
/// Call it as the first statement of the handler, before any load, so a caller without the permission
/// gets a 403 for any ID.
/// </summary>
public interface IPermissionGuard
{
    /// <summary>
    /// Loads the caller. Use it when a rule needs <see cref="Caller.Permissions"/>, for example an entity rule that
    /// depends on the state of the entity, or when the handler makes more than one check. To make one check, use
    /// <see cref="RequireAsync(Permission, CancellationToken)"/>.
    /// </summary>
    Task<Caller> GetCallerAsync(CancellationToken cancellationToken);

    /// <exception cref="ForbiddenAccessException">The caller does not hold the permission.</exception>
    Task RequireAsync(Permission permission, CancellationToken cancellationToken);

    /// <summary>
    /// Passes for the user with <paramref name="userId"/>, a user who acts on their own record.
    /// Any other caller needs the permission.
    /// </summary>
    /// <exception cref="ForbiddenAccessException">The caller is another user and does not hold the permission.</exception>
    Task RequireSelfOrAsync(string userId, Permission permission, CancellationToken cancellationToken);
}

public sealed class PermissionGuard(
    AppDbContext dbContext,
    ICallerIdentityResolver callerIdentityResolver) : IPermissionGuard
{
    public async Task RequireAsync(Permission permission, CancellationToken cancellationToken)
        => (await GetCallerAsync(cancellationToken)).Require(permission);

    public async Task RequireSelfOrAsync(string userId, Permission permission, CancellationToken cancellationToken)
        => (await GetCallerAsync(cancellationToken)).RequireSelfOr(userId, permission);

    public async Task<Caller> GetCallerAsync(CancellationToken cancellationToken)
        => await callerIdentityResolver.GetAsync(cancellationToken) switch
        {
            CallerIdentity.System or CallerIdentity.AllTenantsClient { CanManage: true } => Caller.AllTenantsManager.Instance,
            CallerIdentity.AllTenantsClient => new Caller.Outsider(OutsiderKind.MachineClient),
            CallerIdentity.User user => await LoadMemberAsync(user.Subject, cancellationToken),
            CallerIdentity.UnknownClient => new Caller.Outsider(OutsiderKind.UnknownClient),
            CallerIdentity.Anonymous => new Caller.Outsider(OutsiderKind.Anonymous),
            _ => throw new UnreachableException(),
        };

    private async Task<Caller> LoadMemberAsync(string identifier, CancellationToken cancellationToken)
    {
        // No tracking: a tracked copy would be shared with a handler that loads this user to change it.
        var user = await dbContext.Users
            .TagWithOperationCallSite("users.permission_guard.load")
            .AsNoTracking()
            .Include(u => u.UserPermissions)
            .FirstOrDefaultAsync(u => u.Identifier == identifier, cancellationToken);

        return user is null
            ? new Caller.Outsider(OutsiderKind.NoUserRecord)
            : new Caller.Member(user.Id, user.UserPermissions.Select(p => p.Permission));
    }
}
