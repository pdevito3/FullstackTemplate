namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;

public static class UpdateTenant
{
    public sealed record Command(Guid Id, TenantForUpdateDto Dto) : IRequest<TenantDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, TenantDto>
    {
        public async Task<TenantDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var tenant = await dbContext.Tenants.GetById(request.Id, cancellationToken);

            var forUpdate = request.Dto.ToTenantForUpdate();
            tenant.Update(forUpdate);

            await dbContext.SaveChangesAsync(cancellationToken);

            return tenant.ToTenantDto();
        }
    }
}
