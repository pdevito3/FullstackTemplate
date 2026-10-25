namespace FullstackTemplate.Server.Domain.Users.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Mappings;
using MediatR;
using Resources;
using Serilog;

public static class AddUserPermission
{
    // The endpoint sends the permission of UserPermissionDto, so the rule of the DTO is also on the command
    public sealed record Command(string Id, [property: RequiredField] string Permission) : IRequest<UserDto>;

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
                .TagWithOperationCallSite("users.add_permission.load")
                .GetById(request.Id, cancellationToken);

            var existingPermissionIds = user.UserPermissions.Select(p => p.Id).ToHashSet();

            user.AddPermission(permission);

            // Explicitly track new permissions (needed due to PropertyAccessMode.Field)
            foreach (var userPermission in user.UserPermissions)
            {
                if (!existingPermissionIds.Contains(userPermission.Id))
                {
                    dbContext.UserPermissions.Add(userPermission);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (user.UserPermissions.Count > existingPermissionIds.Count)
                _logger.Here().Information("Added permission {Permission} to user {UserId}", permission.Value, user.Id);

            return user.ToUserDto();
        }
    }
}
