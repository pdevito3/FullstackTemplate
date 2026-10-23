namespace FullstackTemplate.Server.Domain.Agents.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Users;

public static class AddAgentPermission
{
    // The endpoint sends the permission of AgentPermissionDto, so the rule of the DTO is also on the command
    public sealed record Command(string Id, [property: RequiredField] string Permission) : IRequest<AgentDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command, AgentDto>
    {
        public async Task<AgentDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ManageAgents());
            var permission = Permission.Of(request.Permission);

            var agent = await dbContext.Agents
                .TagWithOperationCallSite("agents.add_permission.load")
                .Include(a => a.Permissions)
                .GetById(request.Id, cancellationToken);

            var existingPermissionIds = agent.Permissions.Select(p => p.Id).ToHashSet();

            agent.Grant(permission, caller);

            // Explicitly track new permissions (needed due to PropertyAccessMode.Field)
            foreach (var agentPermission in agent.Permissions)
            {
                if (!existingPermissionIds.Contains(agentPermission.Id))
                {
                    dbContext.AgentPermissions.Add(agentPermission);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return agent.ToAgentDto();
        }
    }
}
