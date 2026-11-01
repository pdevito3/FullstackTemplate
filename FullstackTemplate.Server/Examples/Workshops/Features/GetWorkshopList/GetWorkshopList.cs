namespace FullstackTemplate.Server.Examples.Workshops.Features;

using Databases;
using Domain.Users;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;

public static class GetWorkshopList
{
    public sealed record Query(WorkshopParametersDto Parameters) : IRequest<PagedList<WorkshopDto>>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Query, PagedList<WorkshopDto>>
    {
        public async Task<PagedList<WorkshopDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);

            var queryKitConfig = new CustomQueryKitConfiguration();

            var query = dbContext.Workshops
                .TagWithOperationCallSite("workshops.list")
                .AsNoTracking()
                .ToWorkshopDtoQueryable(dbContext.Venues);

            if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                query = query.ApplyFilters(request.Parameters.Filters, queryKitConfig);

            query = string.IsNullOrWhiteSpace(request.Parameters.SortOrder)
                ? query.OrderBy(w => w.StartsAt).ThenBy(w => w.Id)
                : query.ApplySortOrder(request.Parameters.SortOrder, queryKitConfig).ThenBy(w => w.Id);

            var page = await PagedList.CreateAsync(
                query,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
            page.AddAllowedActions(caller);
            return page;
        }
    }
}
