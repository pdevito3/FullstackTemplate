namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Databases;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Users;

public static class DeleteTenant
{
    public sealed record Command(string Id) : IRequest;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command>
    {
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            await permissionGuard.RequireAsync(Permission.ManageTenant(), cancellationToken);

            var tenant = await dbContext.Tenants
                .TagWithOperationCallSite("tenants.delete.load")
                .GetById(request.Id, cancellationToken);

            // A deleted agent frees its client ID, so the operator can bind a machine client to another tenant.
            var agents = await dbContext.Agents
                .TagWithOperationCallSite("tenants.delete.load_agents")
                .Include(a => a.Permissions)
                .Where(a => a.TenantId == tenant.Id)
                .ToListAsync(cancellationToken);
            foreach (var agent in agents)
            {
                dbContext.AgentPermissions.RemoveRange(agent.Permissions);
                dbContext.Agents.Remove(agent);
            }

            dbContext.Tenants.Remove(tenant);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
