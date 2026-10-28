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

public static class AddUser
{
    public sealed record Command(UserForCreationDto Dto) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command, UserDto>
    {
        // The key in the errors of the problem is the name of the request field
        private const string TenantIdField = "tenantId";

        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            // The requested tenant first, so that a refused request also has it on the span
            Activity.Current?.SetTag("app.tenant_id", request.Dto.TenantId);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ManageUsers());
            var role = UserRole.Of(request.Dto.Role);
            Activity.Current?.SetTag("app.user.role", role.Value);
            caller.Require(role.GetDefaultPermissions());

            var tenantId = await ResolveTenantIdAsync(request.Dto.TenantId, cancellationToken);
            Activity.Current?.SetTag("app.tenant_id", tenantId);

            var forCreation = request.Dto.ToUserForCreation(tenantId);
            var user = User.Create(forCreation);
            Activity.Current?.SetTag("app.user_id", user.Id);

            // Ignore the tenant filter: the unique index on Identifier covers the live users of all tenants.
            var identifierInUse = await dbContext.Users
                .TagWithOperationCallSite("users.add.identifier_in_use")
                .IgnoreQueryFilters([QueryFilterNames.Tenant])
                .AnyAsync(u => u.Identifier == user.Identifier, cancellationToken);
            if (identifierInUse)
                throw new ConflictException("identifier", "A user with this identifier already exists.");

            await dbContext.Users.AddAsync(user, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("Added user {UserId} to tenant {TenantId} with role {Role}", user.Id, tenantId, role.Value);
            
            // TODO handle IDP creation

            return user.ToUserDto(caller);
        }

        // A user adds to their own tenant. A caller that sees all tenants has no tenant of its own,
        // so it must name the tenant.
        private async Task<string> ResolveTenantIdAsync(string? requestedTenantId, CancellationToken cancellationToken)
        {
            var scope = dbContext.CurrentTenantScope;
            if (string.IsNullOrWhiteSpace(requestedTenantId))
            {
                if (scope.TenantId is { } ownTenantId)
                    return ownTenantId;
                if (scope.IncludesAllTenants)
                    throw new ValidationException(TenantIdField, "Please provide a tenant.");
                throw new ForbiddenAccessException("You can add a user only to your own tenant.");
            }

            if (!scope.Includes(requestedTenantId))
                throw new ForbiddenAccessException("You can add a user only to your own tenant.");

            // The tenant filter and the soft delete filter apply, so a deleted tenant does not exist here.
            var tenantExists = EntityId.IsValid(requestedTenantId, Tenants.Tenant.IdPrefix)
                && await dbContext.Tenants
                    .TagWithOperationCallSite("users.add.tenant_exists")
                    .AnyAsync(t => t.Id == requestedTenantId, cancellationToken);
            if (!tenantExists)
                throw new ValidationException(TenantIdField, "The tenant does not exist.");

            return requestedTenantId;
        }
    }
}
