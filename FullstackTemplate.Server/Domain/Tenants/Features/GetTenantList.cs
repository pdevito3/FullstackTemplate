namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QueryKit;
using Resources;

public static class GetTenantList
{
    public sealed record Query(TenantParametersDto Parameters) : IRequest<PagedList<TenantDto>>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, PagedList<TenantDto>>
    {
        public async Task<PagedList<TenantDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var queryKitConfig = new CustomQueryKitConfiguration();

            // The filter and the sort run on the DTO, so a caller can query only the fields that the contract shows
            var query = dbContext.Tenants
                .TagWithOperationCallSite("tenants.list")
                .AsNoTracking()
                .ToTenantDtoQueryable();

            if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                query = query.ApplyQueryKitFilter(request.Parameters.Filters, queryKitConfig);

            // Paging needs a fixed order, or a row can show on two pages or on none
            query = string.IsNullOrWhiteSpace(request.Parameters.SortOrder)
                ? query.OrderBy(t => t.Name).ThenBy(t => t.Id)
                : query.ApplyQueryKitSort(request.Parameters.SortOrder, queryKitConfig);

            return await PagedList<TenantDto>.CreateAsync(
                query,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
        }
    }
}
