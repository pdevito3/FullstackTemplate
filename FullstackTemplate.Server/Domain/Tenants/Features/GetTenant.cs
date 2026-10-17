namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;

public static class GetTenant
{
    public sealed record Query(Guid Id) : IRequest<TenantDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, TenantDto>
    {
        public async Task<TenantDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var tenant = await dbContext.Tenants
                .TagWithOperationCallSite("tenants.get_by_id")
                .GetById(request.Id, cancellationToken);
            return tenant.ToTenantDto();
        }
    }
}
