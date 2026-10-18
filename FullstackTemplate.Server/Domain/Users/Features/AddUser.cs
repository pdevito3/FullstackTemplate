namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class AddUser
{
    public sealed record Command(UserForCreationDto Dto) : IRequest<UserDto>, IRequireTenantAdmin;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, UserDto>
    {
        // The key in the errors of the problem is the name of the request field
        private const string TenantIdField = "tenantId";

        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var tenantId = await ResolveTenantIdAsync(request.Dto.TenantId, cancellationToken);

            // Ignore all filters: the unique index on Identifier also covers other tenants and deleted users.
            var identifierInUse = await dbContext.Users
                .TagWithOperationCallSite("users.add.identifier_in_use")
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Identifier == request.Dto.Identifier, cancellationToken);
            if (identifierInUse)
                throw new ConflictException("identifier", "A user with this identifier already exists.");

            var forCreation = request.Dto.ToUserForCreation(tenantId);
            var user = User.Create(forCreation);

            await dbContext.Users.AddAsync(user, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            
            // TODO handle IDP creation

            return user.ToUserDto();
        }

        // A user adds to their own tenant. A caller that sees all tenants has no tenant of its own,
        // so it must name the tenant.
        private async Task<Guid> ResolveTenantIdAsync(Guid? requestedTenantId, CancellationToken cancellationToken)
        {
            var scope = dbContext.CurrentTenantScope;
            if (requestedTenantId is not { } tenantId)
            {
                if (scope.TenantId is { } ownTenantId)
                    return ownTenantId;
                if (scope.IncludesAllTenants)
                    throw new ValidationException(TenantIdField, "Please provide a tenant.");
                throw new ForbiddenAccessException("You can add a user only to your own tenant.");
            }

            if (!scope.Includes(tenantId))
                throw new ForbiddenAccessException("You can add a user only to your own tenant.");

            // The tenant filter and the soft delete filter apply, so a deleted tenant does not exist here.
            var tenantExists = await dbContext.Tenants
                .TagWithOperationCallSite("users.add.tenant_exists")
                .AnyAsync(t => t.Id == tenantId, cancellationToken);
            if (!tenantExists)
                throw new ValidationException(TenantIdField, "The tenant does not exist.");

            return tenantId;
        }
    }
}
