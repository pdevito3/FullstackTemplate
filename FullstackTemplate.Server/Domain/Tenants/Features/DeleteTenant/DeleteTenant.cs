namespace FullstackTemplate.Server.Domain.Tenants.Features;

using System.Diagnostics;
using Databases;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;
using Users;

public static class DeleteTenant
{
    public sealed record Command(string Id) : IRequest, IRunsAgainWhenRaced;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.tenant_id", request.Id);

            await permissionGuard.RequireAsync(Permission.ManageTenant(), cancellationToken);

            // The exclusive lock waits for each agent add, user add, or link add to a user of the tenant, so no
            // agent, user, or link comes after the load. A deleted user frees its identifier, so the same person can
            // sign up again.
            var (agentCount, userCount) = await RootLock.Tenant.RunExclusiveAsync(dbContext, request.Id, async ct =>
            {
                var tenant = await dbContext.Tenants
                    .TagWithOperationCallSite("tenants.delete.load")
                    .GetById(request.Id, ct);

                var agents = await dbContext.Agents
                    .TagWithOperationCallSite("tenants.delete.load_agents")
                    .Include(a => a.Permissions)
                    .Where(a => a.TenantId == tenant.Id)
                    .ToListAsync(ct);
                foreach (var agent in agents)
                {
                    dbContext.AgentPermissions.RemoveRange(agent.Permissions);
                    dbContext.Agents.Remove(agent);
                }

                var users = await dbContext.Users
                    .TagWithOperationCallSite("tenants.delete.load_users")
                    .Include(u => u.UserPermissions)
                    .Where(u => u.TenantId == tenant.Id)
                    .ToListAsync(ct);
                foreach (var user in users)
                {
                    dbContext.UserPermissions.RemoveRange(user.UserPermissions);
                    dbContext.Users.Remove(user.Delete());
                }

                dbContext.Tenants.Remove(tenant);
                await dbContext.SaveChangesAsync(ct);
                return (agents.Count, users.Count);
            }, cancellationToken);

            _logger.Here().Information("Deleted tenant {TenantId}, {AgentCount} agents, and {UserCount} users",
                request.Id, agentCount, userCount);
        }
    }
}
