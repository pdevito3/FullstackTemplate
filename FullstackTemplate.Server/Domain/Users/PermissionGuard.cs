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
            { CanManageAllTenants: true } => Caller.AllTenantsManager.Instance,
            CallerIdentity.AllTenantsClient => new Caller.Outsider(OutsiderKind.MachineClient),
            CallerIdentity.User user => await LoadUserAsync(user.Subject, cancellationToken) is { } member
                ? new Caller.Member(member.Id, member.UserPermissions.Select(p => p.Permission))
                : new Caller.Outsider(OutsiderKind.NoUserRecord),
            CallerIdentity.DelegatedAgent agent => await LoadDelegatedAgentAsync(agent, cancellationToken),
            CallerIdentity.MachineAgent agent => await LoadGrantAsync(agent.AgentId, cancellationToken) is { } grant
                ? new Caller.MachineAgent(agent.AgentId, agent.ClientId, grant)
                : new Caller.Outsider(OutsiderKind.UnknownClient),
            CallerIdentity.UnknownClient => new Caller.Outsider(OutsiderKind.UnknownClient),
            CallerIdentity.Anonymous => new Caller.Outsider(OutsiderKind.Anonymous),
            _ => throw new UnreachableException(),
        };

    private async Task<Caller> LoadDelegatedAgentAsync(
        CallerIdentity.DelegatedAgent agent, CancellationToken cancellationToken)
    {
        if (await LoadUserAsync(agent.Subject, cancellationToken) is not { } user)
            return new Caller.Outsider(OutsiderKind.NoUserRecord);

        if (await LoadGrantAsync(agent.AgentId, cancellationToken) is not { } grant)
            return new Caller.Outsider(OutsiderKind.UnknownClient);

        return new Caller.DelegatedAgent(
            agent.AgentId, agent.ClientId, user.Id, user.UserPermissions.Select(p => p.Permission), grant);
    }

    // No tracking: a tracked copy would be shared with a handler that loads this user to change it.
    private Task<User?> LoadUserAsync(string identifier, CancellationToken cancellationToken)
        => dbContext.Users
            .TagWithOperationCallSite("users.permission_guard.load")
            .AsNoTracking()
            .Include(u => u.UserPermissions)
            .FirstOrDefaultAsync(u => u.Identifier == identifier, cancellationToken);

    // Null when the agent or its tenant is deleted. The identity of the scope can be older than this check.
    private async Task<IEnumerable<Permission>?> LoadGrantAsync(string agentId, CancellationToken cancellationToken)
    {
        var agent = await dbContext.Agents
            .TagWithOperationCallSite("agents.permission_guard.load")
            .AsNoTracking()
            .Include(a => a.Permissions)
            .Where(a => a.Id == agentId && dbContext.Tenants.Any(t => t.Id == a.TenantId))
            .FirstOrDefaultAsync(cancellationToken);

        return agent?.Permissions.Select(p => p.Permission);
    }
}
