namespace FullstackTemplate.Server.Domain.Agents.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Users;

public static class RemoveAgentPermission
{
    public sealed record Command(string Id, string Permission) : IRequest<AgentDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command, AgentDto>
    {
        public async Task<AgentDto> Handle(Command request, CancellationToken cancellationToken)
        {
            await permissionGuard.RequireAsync(Permission.ManageAgents(), cancellationToken);
            var permission = Permission.Of(request.Permission);

            var agent = await dbContext.Agents
                .TagWithOperationCallSite("agents.remove_permission.load")
                .Include(a => a.Permissions)
                .GetById(request.Id, cancellationToken);

            var existingPermissions = agent.Permissions.ToList();

            agent.Revoke(permission);

            var remainingIds = agent.Permissions.Select(p => p.Id).ToHashSet();
            foreach (var existingPermission in existingPermissions)
            {
                if (!remainingIds.Contains(existingPermission.Id))
                {
                    dbContext.AgentPermissions.Remove(existingPermission);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return agent.ToAgentDto();
        }
    }
}
