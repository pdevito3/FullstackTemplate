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

            IQueryable<Tenant> query = dbContext.Tenants
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                query = query.ApplyQueryKitFilter(request.Parameters.Filters, queryKitConfig);

            if (!string.IsNullOrWhiteSpace(request.Parameters.SortOrder))
                query = query.ApplyQueryKitSort(request.Parameters.SortOrder, queryKitConfig);

            var dtos = query.ToTenantDtoQueryable();

            return await PagedList<TenantDto>.CreateAsync(
                dtos,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
        }
    }
}
