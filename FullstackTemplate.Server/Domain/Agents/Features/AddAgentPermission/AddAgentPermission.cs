namespace FullstackTemplate.Server.Domain.Agents.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;
using Users;

public static class AddAgentPermission
{
    public sealed record Command(string Id, [property: RequiredField] string Permission) : IRequest<AgentDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command, AgentDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<AgentDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.agent_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ManageAgents());
            var permission = Permission.Of(request.Permission);
            Activity.Current?.SetTag("app.permission", permission.Value);

            var agent = await dbContext.Agents
                .TagWithOperationCallSite("agents.add_permission.load")
                .Include(a => a.Permissions)
                .GetById(request.Id, cancellationToken);

            var existingPermissionIds = agent.Permissions.Select(p => p.Id).ToHashSet();

            agent.Grant(permission, caller);

            foreach (var agentPermission in agent.Permissions)
            {
                if (!existingPermissionIds.Contains(agentPermission.Id))
                {
                    dbContext.AgentPermissions.Add(agentPermission);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (agent.Permissions.Count > existingPermissionIds.Count)
                _logger.Here().Information("Added permission {Permission} to agent {AgentId}", permission.Value, agent.Id);

            return agent.ToAgentDto();
        }
    }
}
