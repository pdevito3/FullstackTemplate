namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Tenants;
using Tenants.Mappings;
using Tenants.Models;
using Models;

public static class InitiateUserAndTenant
{
    public sealed record Command(InitiateUserAndTenantDto Dto) : IRequest<InitiateUserAndTenantResponseDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, InitiateUserAndTenantResponseDto>
    {
        public async Task<InitiateUserAndTenantResponseDto> Handle(Command request, CancellationToken cancellationToken)
        {
            // Create tenant first
            var tenantForCreation = new TenantForCreation { Name = request.Dto.TenantName };
            var tenant = Tenant.Create(tenantForCreation);
            await dbContext.Tenants.AddAsync(tenant, cancellationToken);

            // Create user with tenant reference
            var userForCreation = new UserForCreation
            {
                TenantId = tenant.Id,
                FirstName = request.Dto.FirstName,
                LastName = request.Dto.LastName,
                Identifier = request.Dto.Identifier,
                Email = request.Dto.Email,
                Username = request.Dto.Username,
                Role = request.Dto.Role
            };
            var user = User.Create(userForCreation);
            await dbContext.Users.AddAsync(user, cancellationToken);

            // Single transaction - both saved together
            await dbContext.SaveChangesAsync(cancellationToken);

            return new InitiateUserAndTenantResponseDto
            {
                Tenant = tenant.ToTenantDto(),
                User = user.ToUserDto()
            };
        }
    }
}
