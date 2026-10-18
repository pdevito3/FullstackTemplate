namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
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

            var forUpdate = request.Dto.ToTenantForUpdate();
            tenant.Update(forUpdate);

            await dbContext.SaveChangesAsync(cancellationToken);

            return tenant.ToTenantDto();
        }
    }
}
