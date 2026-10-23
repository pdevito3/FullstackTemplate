namespace FullstackTemplate.Server.Domain.Agents.Features;

using Databases;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Users;

public static class DeleteAgent
{
    public sealed record Command(string Id) : IRequest;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command>
    {
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ManageAgents());

            var agent = await dbContext.Agents
                .TagWithOperationCallSite("agents.delete.load")
                .Include(a => a.Permissions)
                .GetById(request.Id, cancellationToken);
            // A delete takes away each permission of the grant.
            caller.Require(agent.Permissions.Select(p => p.Permission));

            // Remove permissions first (due to PropertyAccessMode.Field)
            foreach (var permission in agent.Permissions.ToList())
            {
                dbContext.AgentPermissions.Remove(permission);
            }

            dbContext.Agents.Remove(agent);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
