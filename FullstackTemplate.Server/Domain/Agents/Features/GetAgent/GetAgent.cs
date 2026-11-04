namespace FullstackTemplate.Server.Domain.Agents.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Users;

public static class GetAgent
{
    public sealed record Query(string Id) : IRequest<AgentDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Query, AgentDto>
    {
        public async Task<AgentDto> Handle(Query request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.agent_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ReadAgents());

            var agent = await dbContext.Agents
                .TagWithOperationCallSite("agents.get_by_id")
                .Include(a => a.Permissions)
                .GetById(request.Id, cancellationToken);
            return agent.ToAgentDto(caller);
        }
    }
}
