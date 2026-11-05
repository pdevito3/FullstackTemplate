namespace FullstackTemplate.Server.Domain.Users.Features;

using System.Diagnostics;
using Databases;
using Domain.Tenants;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class GetUserByIdentifier
{
    public sealed record Query(string Identifier) : IRequest<UserDto>;

    public sealed class Handler(
        AppDbContext dbContext,
        IPermissionGuard permissionGuard,
        ICallerIdentityResolver callerIdentityResolver)
        : IRequestHandler<Query, UserDto>
    {
        public async Task<UserDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);

            // Postgres cannot take a NUL or a lone surrogate as a parameter, and no stored identifier has one
            var user = PostgresText.CanStore(request.Identifier)
                ? await dbContext.Users
                    .TagWithOperationCallSite("users.get_by_identifier")
                    .AsNoTracking()
                    .Include(u => u.UserPermissions)
                    .FirstOrDefaultAsync(u => u.Identifier == request.Identifier, cancellationToken)
                : null;
            Activity.Current?.SetTag("app.user_id", user?.Id);

            // The check follows the load, because the exception for the own record needs the ID of the user
            if (user is null)
            {
                // A user who reads their own identifier learns nothing from the 404. A new user knows to start a tenant
                // from it. A delete can commit after this request found the caller, and the next request gets this 404.
                var readsOwnIdentifier = await callerIdentityResolver.GetAsync(cancellationToken)
                    is CallerIdentity.User { Subject: var subject } && subject == request.Identifier;

                // A user with no user record and no tenant sees no rows, so the 404 tells it nothing. The scope is
                // checked too, because a cached tenant of a user who was just deleted can still give rows.
                var seesNoRows = caller is Caller.Outsider { Kind: OutsiderKind.NoUserRecord }
                    && dbContext.CurrentTenantScope == TenantScope.NoTenant;
                if (!readsOwnIdentifier && !seesNoRows)
                    caller.Require(Permission.ReadUsers());
                throw new NotFoundException("User was not found.");
            }

            caller.RequireSelfOr(user.Id, Permission.ReadUsers());
            return user.ToUserDto(caller);
        }
    }
}
