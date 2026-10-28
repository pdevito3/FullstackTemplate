namespace FullstackTemplate.Server.Examples.Venues.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;

public static class GetVenueList
{
    public sealed record Query(VenueParametersDto Parameters) : IRequest<PagedList<VenueDto>>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, PagedList<VenueDto>>
    {
        public async Task<PagedList<VenueDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var queryKitConfig = new CustomQueryKitConfiguration();

            var query = dbContext.Venues
                .TagWithOperationCallSite("venues.list")
                .AsNoTracking()
                .ToVenueDtoQueryable();

            if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                query = query.ApplyFilters(request.Parameters.Filters, queryKitConfig);

            query = string.IsNullOrWhiteSpace(request.Parameters.SortOrder)
                ? query.OrderBy(v => v.Name).ThenBy(v => v.Id)
                : query.ApplySortOrder(request.Parameters.SortOrder, queryKitConfig).ThenBy(v => v.Id);

            return await PagedList.CreateAsync(
                query,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
        }
    }
}
