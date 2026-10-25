namespace FullstackTemplate.Server.Domain.Users.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;

public static class UpdateUserRole
{
    // The endpoint sends the role of UpdateUserRoleDto, so the rule of the DTO is also on the command
    public sealed record Command(string Id, [property: RequiredField] string Role) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command, UserDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.user_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ManageUserAccess());
            // The response carries the record of the user, so it needs the read permission too (ADR 0009).
            caller.RequireSelfOr(request.Id, Permission.ReadUsers());
            var newRole = UserRole.Of(request.Role);
            Activity.Current?.SetTag("app.user.role", newRole.Value);
            caller.Require(newRole.GetDefaultPermissions());

            var user = await dbContext.Users
                .TagWithOperationCallSite("users.update_role.load")
                .Include(u => u.UserPermissions)
                .GetById(request.Id, cancellationToken);
            // The new role replaces each permission row of the user, so the caller also takes those away.
            caller.Require(user.UserPermissions.Select(p => p.Permission));

            var existingPermissions = user.UserPermissions.ToList();
            var existingPermissionIds = existingPermissions.Select(p => p.Id).ToHashSet();

            var oldRole = user.Role;
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

            if (newRole != oldRole)
            {
                _logger.Here().Information("Changed the role of user {UserId} to {Role}", user.Id, newRole.Value);
                UserMetrics.RoleChanged.Add(1, new KeyValuePair<string, object?>("app.user.role", newRole.Value));
            }

            return user.ToUserDto();
        }
    }
}
