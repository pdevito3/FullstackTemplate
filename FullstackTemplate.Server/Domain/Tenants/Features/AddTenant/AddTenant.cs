namespace FullstackTemplate.Server.Domain.Tenants.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;
using Users;

public static class AddTenant
{
    public sealed record Command(TenantForCreationDto Dto) : IRequest<TenantDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Command, TenantDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<TenantDto> Handle(Command request, CancellationToken cancellationToken)
        {
            // A new tenant is outside the scope of each user, so only a caller that can manage all tenants
            // can add one: a trusted tenant management client, or system work in a system scope.
            // A user starts their own tenant with InitiateUserAndTenant.
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            if (!caller.ManagesAllTenants)
                throw new ForbiddenAccessException(
                    "Only a trusted tenant management client can add a tenant. A user starts their own tenant with POST /api/v1/users/initiate.");

            var forCreation = request.Dto.ToTenantForCreation();
            var tenant = Tenant.Create(forCreation);
            Activity.Current?.SetTag("app.tenant_id", tenant.Id);

            // Ignore all filters: the unique index on Name also covers deleted tenants.
            var nameInUse = await dbContext.Tenants
                .TagWithOperationCallSite("tenants.add.name_in_use")
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Name == tenant.Name, cancellationToken);
            if (nameInUse)
                throw new ConflictException("name", "A tenant with this name already exists.");

            await dbContext.Tenants.AddAsync(tenant, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("Added tenant {TenantId}", tenant.Id);

            return tenant.ToTenantDto();
        }
    }
}
