namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;
using Services;
using Tenants;
using Tenants.Mappings;
using Tenants.Models;
using Models;

public static class InitiateUserAndTenant
{
    public sealed record Command(InitiateUserAndTenantDto Dto) : IRequest<InitiateUserAndTenantResponseDto>;

    public sealed class Handler(
        AppDbContext dbContext,
        ICurrentUserService currentUserService,
        ITenantScopeProvider tenantScopeProvider)
        : IRequestHandler<Command, InitiateUserAndTenantResponseDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<InitiateUserAndTenantResponseDto> Handle(Command request, CancellationToken cancellationToken)
        {
            GuardCallerCanInitiateFor(request.Dto.Identifier);

            // Ignore all filters: the unique index on Identifier also covers other tenants and deleted users.
            var identifierInUse = await dbContext.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Identifier == request.Dto.Identifier, cancellationToken);
            if (identifierInUse)
                throw new ConflictException("identifier", "A user with this identifier already exists.");

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

        // A signed-in user can start a tenant only for themselves. A caller with no user identifier must be
        // able to manage all tenants: a trusted tenant management client, or system work in a system scope.
        private void GuardCallerCanInitiateFor(string identifier)
        {
            var callerIdentifier = currentUserService.UserIdentifier;
            if (callerIdentifier is null)
            {
                if (!tenantScopeProvider.CanManageAllTenants())
                    throw new ForbiddenAccessException(
                        "A caller with no user identifier must be a trusted tenant management client to start a tenant.");
                return;
            }

            if (callerIdentifier != identifier)
                throw new ForbiddenAccessException("You can start a tenant only for yourself.");
        }
    }
}
