namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Databases;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Services;

public static class AddTenant
{
    public sealed record Command(TenantForCreationDto Dto) : IRequest<TenantDto>;

    public sealed class Handler(AppDbContext dbContext, ITenantScopeProvider tenantScopeProvider)
        : IRequestHandler<Command, TenantDto>
    {
        public async Task<TenantDto> Handle(Command request, CancellationToken cancellationToken)
        {
            // A new tenant is outside the scope of each user, so only a caller that can manage all tenants
            // can add one: a trusted tenant management client, or system work in a system scope.
            // A user starts their own tenant with InitiateUserAndTenant.
            if (!tenantScopeProvider.CanManageAllTenants())
                throw new ForbiddenAccessException(
                    "Only a trusted tenant management client can add a tenant. A user starts their own tenant with POST /api/v1/users/initiate.");

            // Ignore all filters: the unique index on Name also covers deleted tenants.
            var nameInUse = await dbContext.Tenants
                .TagWithOperationCallSite("tenants.add.name_in_use")
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Name == request.Dto.Name, cancellationToken);
            if (nameInUse)
                throw new ConflictException("name", "A tenant with this name already exists.");

            var forCreation = request.Dto.ToTenantForCreation();
            var tenant = Tenant.Create(forCreation);

            await dbContext.Tenants.AddAsync(tenant, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return tenant.ToTenantDto();
        }
    }
}
