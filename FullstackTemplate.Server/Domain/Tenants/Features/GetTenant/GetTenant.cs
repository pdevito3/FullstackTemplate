namespace FullstackTemplate.Server.Domain.Tenants.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Mappings;
using MediatR;
using Users;

public static class GetTenant
{
    public sealed record Query(string Id) : IRequest<TenantDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Query, TenantDto>
    {
        public async Task<TenantDto> Handle(Query request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.tenant_id", request.Id);

            // Each caller can read a tenant that the tenant filter shows. The caller decides only the actions.
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            var tenant = await dbContext.Tenants
                .TagWithOperationCallSite("tenants.get_by_id")
                .GetById(request.Id, cancellationToken);
            return tenant.ToTenantDto(caller);
        }
    }
}
