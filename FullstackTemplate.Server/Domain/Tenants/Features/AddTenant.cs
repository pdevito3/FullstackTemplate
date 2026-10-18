namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Databases;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;

public static class AddTenant
{
    public sealed record Command(TenantForCreationDto Dto) : IRequest<TenantDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, TenantDto>
    {
        public async Task<TenantDto> Handle(Command request, CancellationToken cancellationToken)
        {
            // A new tenant is outside the scope of each user, so only a caller with all-tenant access can add one.
            // A user starts their own tenant with InitiateUserAndTenant.
            if (!dbContext.CurrentTenantScope.IncludesAllTenants)
                throw new ForbiddenAccessException("Only a trusted machine client can add a tenant.");

            var forCreation = request.Dto.ToTenantForCreation();
            var tenant = Tenant.Create(forCreation);

            await dbContext.Tenants.AddAsync(tenant, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return tenant.ToTenantDto();
        }
    }
}
