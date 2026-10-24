namespace FullstackTemplate.Server.Domain.Users.Features;

using System.Diagnostics;
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
        ICallerIdentityResolver callerIdentityResolver)
        : IRequestHandler<Command, InitiateUserAndTenantResponseDto>
    {
        private const string TenantNameField = "tenantName";
        private const string TenantNameTaken = "A tenant with this name already exists.";

        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<InitiateUserAndTenantResponseDto> Handle(Command request, CancellationToken cancellationToken)
        {
            await GuardCallerCanInitiateForAsync(request.Dto.Identifier, cancellationToken);

            // Ignore all filters: the unique index on Identifier also covers other tenants and deleted users.
            var identifierInUse = await dbContext.Users
                .TagWithOperationCallSite("users.initiate.identifier_in_use")
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Identifier == request.Dto.Identifier, cancellationToken);
            if (identifierInUse)
                throw new ConflictException("identifier", "A user with this identifier already exists.");

            // Ignore all filters: the unique index on Name also covers deleted tenants.
            var tenantNameInUse = await dbContext.Tenants
                .TagWithOperationCallSite("users.initiate.tenant_name_in_use")
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Name == request.Dto.TenantName, cancellationToken);
            if (tenantNameInUse)
                throw new ConflictException(TenantNameField, TenantNameTaken);

            // Create tenant first
            var tenantForCreation = new TenantForCreation { Name = request.Dto.TenantName };
            Tenant tenant;
            try
            {
                tenant = Tenant.Create(tenantForCreation);
            }
            // The tenant names the field name, but this request names it tenantName
            catch (ValidationException exception) when (exception.Property == "name")
            {
                throw new ValidationException(TenantNameField, exception.Message);
            }
            Activity.Current?.SetTag("app.tenant_id", tenant.Id);
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
            Activity.Current?.SetTag("app.user_id", user.Id);
            await dbContext.Users.AddAsync(user, cancellationToken);

            // Single transaction - both saved together
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            // Two requests can pass the check at the same time. Then the index stops the second save.
            catch (ConstraintConflictException exception) when (exception.Constraint == "ix_tenants_name")
            {
                throw new ConflictException(TenantNameField, TenantNameTaken);
            }

            // A new tenant is a business event that someone reads later, so it is a log, not only a span.
            // Ids only: the names and the email in the request are personal data.
            _logger.Here().Information("Created tenant {TenantId} for new user {UserId}", tenant.Id, user.Id);

            return new InitiateUserAndTenantResponseDto
            {
                Tenant = tenant.ToTenantDto(),
                User = user.ToUserDto()
            };
        }

        private async Task GuardCallerCanInitiateForAsync(string identifier, CancellationToken cancellationToken)
        {
            var caller = await callerIdentityResolver.GetAsync(cancellationToken);
            if (caller.CanManageAllTenants)
                return;

            if (caller is not CallerIdentity.User user)
                throw new ForbiddenAccessException(
                    "Only a user who signs in through this app, or a trusted tenant management client, can start a tenant.");

            if (user.Subject != identifier)
                throw new ForbiddenAccessException("You can start a tenant only for yourself.");
        }
    }
}
