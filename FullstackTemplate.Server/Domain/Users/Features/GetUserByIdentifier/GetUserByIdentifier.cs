namespace FullstackTemplate.Server.Domain.Users.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class GetUserByIdentifier
{
    public sealed record Query(string Identifier) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Query, UserDto>
    {
        public async Task<UserDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);

            var user = await dbContext.Users
                .TagWithOperationCallSite("users.get_by_identifier")
                .AsNoTracking()
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Identifier == request.Identifier, cancellationToken);
            Activity.Current?.SetTag("app.user_id", user?.Id);

            // The check follows the load, because the exception for the own record needs the ID of the user
            if (user is null)
            {
                // A user with no user record sees no rows, so the 404 tells it nothing. A new user who reads their own
                // identifier gets the 404 and knows to start a tenant.
                if (caller is not Caller.Outsider { Kind: OutsiderKind.NoUserRecord })
                    caller.Require(Permission.ReadUsers());
                throw new NotFoundException("User was not found.");
            }

            caller.RequireSelfOr(user.Id, Permission.ReadUsers());
            return user.ToUserDto();
        }
    }
}
