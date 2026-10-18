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
    public sealed record Command(string Id, TenantForUpdateDto Dto) : IRequest<TenantDto>, IRequireTenantAdmin;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, TenantDto>
    {
        public async Task<TenantDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var tenant = await dbContext.Tenants
                .TagWithOperationCallSite("tenants.update.load")
                .GetById(request.Id, cancellationToken);

            // Ignore all filters: the unique index on Name also covers deleted tenants and the tenants of other users.
            var nameInUse = await dbContext.Tenants
                .TagWithOperationCallSite("tenants.update.name_in_use")
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Id != tenant.Id && t.Name == request.Dto.Name, cancellationToken);
            if (nameInUse)
                throw new ConflictException("name", "A tenant with this name already exists.");

            var forUpdate = request.Dto.ToTenantForUpdate();
            tenant.Update(forUpdate);

            await dbContext.SaveChangesAsync(cancellationToken);

            return tenant.ToTenantDto();
        }
    }
}
