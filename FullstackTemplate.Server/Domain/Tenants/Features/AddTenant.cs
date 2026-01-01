namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;

public static class AddTenant
{
    public sealed record Command(TenantForCreationDto Dto) : IRequest<TenantDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, TenantDto>
    {
        public async Task<TenantDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var forCreation = request.Dto.ToTenantForCreation();
            var tenant = Tenant.Create(forCreation);

            await dbContext.Tenants.AddAsync(tenant, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return tenant.ToTenantDto();
        }
    }
}
