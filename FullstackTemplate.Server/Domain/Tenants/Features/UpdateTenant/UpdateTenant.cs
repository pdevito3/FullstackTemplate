namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Databases;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Users;

public static class UpdateTenant
{
    public sealed record Command(string Id, TenantForUpdateDto Dto) : IRequest<TenantDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command, TenantDto>
    {
        public async Task<TenantDto> Handle(Command request, CancellationToken cancellationToken)
        {
            await permissionGuard.RequireAsync(Permission.ManageTenant(), cancellationToken);

            var tenant = await dbContext.Tenants
                .TagWithOperationCallSite("tenants.update.load")
                .GetById(request.Id, cancellationToken);

            // The update checks the version first, so a stale form gets the stale version and not a name conflict
            var forUpdate = request.Dto.ToTenantForUpdate();
            tenant.Update(forUpdate);

            // Ignore all filters: the unique index on Name also covers deleted tenants and the tenants of other users.
            var nameInUse = await dbContext.Tenants
                .TagWithOperationCallSite("tenants.update.name_in_use")
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Id != tenant.Id && t.Name == request.Dto.Name, cancellationToken);
            if (nameInUse)
                throw new ConflictException("name", "A tenant with this name already exists.");

            await dbContext.SaveChangesAsync(cancellationToken);

            return tenant.ToTenantDto();
        }
    }
}
