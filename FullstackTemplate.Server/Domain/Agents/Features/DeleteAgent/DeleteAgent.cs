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
            await permissionGuard.RequireAsync(Permission.ManageAgents(), cancellationToken);

            var agent = await dbContext.Agents
                .TagWithOperationCallSite("agents.delete.load")
                .Include(a => a.Permissions)
                .GetById(request.Id, cancellationToken);

            foreach (var permission in agent.Permissions.ToList())
            {
                dbContext.AgentPermissions.Remove(permission);
            }

            dbContext.Agents.Remove(agent);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
