namespace FullstackTemplate.Server.Domain.Agents.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Users;

public static class GetAgentList
{
    public sealed record Query(AgentParametersDto Parameters) : IRequest<PagedList<AgentDto>>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Query, PagedList<AgentDto>>
    {
        public async Task<PagedList<AgentDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ReadAgents());

            var queryKitConfig = new CustomQueryKitConfiguration();

            var query = dbContext.Agents
                .TagWithOperationCallSite("agents.list")
                .AsNoTracking()
                .ToAgentDtoWithoutActionsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                query = query.ApplyFilters(request.Parameters.Filters, queryKitConfig);

            query = string.IsNullOrWhiteSpace(request.Parameters.SortOrder)
                ? query.OrderBy(a => a.Name).ThenBy(a => a.Id)
                : query.ApplySortOrder(request.Parameters.SortOrder, queryKitConfig).ThenBy(a => a.Id);

            var agents = await PagedList.CreateAsync(
                query,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
            agents.AddAllowedActions(caller);
            return agents;
        }
    }
}
