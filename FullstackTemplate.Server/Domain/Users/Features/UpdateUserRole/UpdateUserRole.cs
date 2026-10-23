namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;

public static class UpdateUserRole
{
    // The endpoint sends the role of UpdateUserRoleDto, so the rule of the DTO is also on the command
    public sealed record Command(string Id, [property: RequiredField] string Role) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command, UserDto>
    {
        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ManageUserAccess());
            var newRole = UserRole.Of(request.Role);
            caller.Require(newRole.GetDefaultPermissions());

            var user = await dbContext.Users
                .TagWithOperationCallSite("users.update_role.load")
                .Include(u => u.UserPermissions)
                .GetById(request.Id, cancellationToken);
            // The new role replaces each permission row of the user, so the caller also takes those away.
            caller.Require(user.UserPermissions.Select(p => p.Permission));

            var existingPermissions = user.UserPermissions.ToList();
            var existingPermissionIds = existingPermissions.Select(p => p.Id).ToHashSet();

            user.UpdateRole(newRole);

            // Handle permission changes: remove old, add new
            var remainingIds = user.UserPermissions.Select(p => p.Id).ToHashSet();

            // Remove permissions that were removed from the collection
            foreach (var existingPermission in existingPermissions)
            {
                if (!remainingIds.Contains(existingPermission.Id))
                {
                    dbContext.UserPermissions.Remove(existingPermission);
                }
            }

            // Add new permissions that weren't already tracked
            foreach (var permission in user.UserPermissions)
            {
                if (!existingPermissionIds.Contains(permission.Id))
                {
                    dbContext.UserPermissions.Add(permission);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return user.ToUserDto();
        }
    }
}
