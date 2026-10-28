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
            var managesAllTenants = await GuardCallerCanInitiateForAsync(request.Dto.Identifier, cancellationToken);

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

            // Ignore the tenant filter: the unique index on Identifier covers the live users of all tenants.
            var identifierInUse = await dbContext.Users
                .TagWithOperationCallSite("users.initiate.identifier_in_use")
                .IgnoreQueryFilters([QueryFilterNames.Tenant])
                .AnyAsync(u => u.Identifier == user.Identifier, cancellationToken);
            if (identifierInUse)
                throw new ConflictException("identifier", "A user with this identifier already exists.");

            // Ignore all filters: the unique index on Name also covers deleted tenants.
            var tenantNameInUse = await dbContext.Tenants
                .TagWithOperationCallSite("users.initiate.tenant_name_in_use")
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Name == tenant.Name, cancellationToken);
            if (tenantNameInUse)
                throw new ConflictException(TenantNameField, TenantNameTaken);

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

            // The caller is a client that manages all tenants, or the new user, who has a user record only now
            Caller caller = managesAllTenants
                ? Caller.AllTenantsManager.Instance
                : new Caller.Member(user.Id, user.UserPermissions.Select(p => p.Permission));
            return new InitiateUserAndTenantResponseDto
            {
                Tenant = tenant.ToTenantDto(),
                User = user.ToUserDto(caller)
            };
        }

        /// <returns>True when the caller manages all tenants. False when the caller starts a tenant for itself.</returns>
        private async Task<bool> GuardCallerCanInitiateForAsync(string identifier, CancellationToken cancellationToken)
        {
            var caller = await callerIdentityResolver.GetAsync(cancellationToken);
            if (caller.CanManageAllTenants)
                return true;

            if (caller is not CallerIdentity.User user)
                throw new ForbiddenAccessException(
                    "Only a user who signs in through this app, or a trusted tenant management client, can start a tenant.");

            if (user.Subject != identifier)
                throw new ForbiddenAccessException("You can start a tenant only for yourself.");

            return false;
        }
    }
}
