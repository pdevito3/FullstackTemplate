namespace FullstackTemplate.Server.Domain.Users.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;

public static class RemoveUserPermission
{
    public sealed record Command(string Id, string Permission) : IRequest<UserDto>;

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
            var permission = Permission.Of(request.Permission);
            Activity.Current?.SetTag("app.permission.name", permission.Value);
            caller.Require(permission);

            var user = await dbContext.Users
                .TagWithOperationCallSite("users.remove_permission.load")
                .Include(u => u.UserPermissions)
                .GetById(request.Id, cancellationToken);

            var existingPermissions = user.UserPermissions.ToList();

            user.RemovePermission(permission);

            // Explicitly remove permissions that were removed from the collection
            var remainingIds = user.UserPermissions.Select(p => p.Id).ToHashSet();
            foreach (var existingPermission in existingPermissions)
            {
                if (!remainingIds.Contains(existingPermission.Id))
                {
                    dbContext.UserPermissions.Remove(existingPermission);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (user.UserPermissions.Count < existingPermissions.Count)
                _logger.Here().Information("Removed permission {Permission} from user {UserId}", permission.Value, user.Id);

            return user.ToUserDto();
        }
    }
}
