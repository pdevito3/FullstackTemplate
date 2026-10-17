namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Resources;
using Serilog;
using Tenants;
using Tenants.Mappings;
using Tenants.Models;
using Models;

public static class InitiateUserAndTenant
{
    public sealed record Command(InitiateUserAndTenantDto Dto) : IRequest<InitiateUserAndTenantResponseDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, InitiateUserAndTenantResponseDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

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

            // A new tenant is a business event that someone reads later, so it is a log, not only a span.
            // Ids only: the names and the email in the request are personal data.
            _logger.Here().Information("Created tenant {TenantId} for new user {UserId}", tenant.Id, user.Id);

            return new InitiateUserAndTenantResponseDto
            {
                Tenant = tenant.ToTenantDto(),
                User = user.ToUserDto()
            };
        }
    }
}
