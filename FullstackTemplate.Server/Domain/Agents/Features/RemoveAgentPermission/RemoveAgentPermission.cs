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

public static class RemoveAgentPermission
{
    public sealed record Command(string Id, string Permission) : IRequest<AgentDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command, AgentDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<AgentDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.agent_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ManageAgents());
            // The response carries the record of the agent, so it needs the read permission too (ADR 0009).
            caller.Require(Permission.ReadAgents());
            var permission = Permission.Of(request.Permission);
            Activity.Current?.SetTag("app.permission.name", permission.Value);

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

            if (agent.Permissions.Count < existingPermissions.Count)
                _logger.Here().Information("Removed permission {Permission} from agent {AgentId}", permission.Value, agent.Id);

            return agent.ToAgentDto();
        }
    }
}
