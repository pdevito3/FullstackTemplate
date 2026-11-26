namespace FullstackTemplate.Server.Domain.Users.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;

public static class AddUserPermission
{
    // The endpoint sends the permission of UserPermissionDto, so the rule of the DTO is also on the command
    public sealed record Command(string Id, [property: RequiredField] string Permission) : IRequest<UserDto>, IRunsAgainWhenRaced;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command, UserDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.user_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            UserAction.AddPermission().GuardBeforeLoad(caller, request.Id);
            var permission = Permission.Of(request.Permission);
            Activity.Current?.SetTag("app.permission.name", permission.Value);
            caller.Require(permission);

            var user = await dbContext.Users
                .TagWithOperationCallSite("users.add_permission.load")
                .Include(u => u.UserRoles)
                .Include(u => u.PermissionOverrides)
                .Include(u => u.UserPermissions)
                .GetById(request.Id, cancellationToken);
            user.GuardAllows(UserAction.AddPermission(), caller);

            var hadPermission = user.HasPermission(permission);
            dbContext.ChangeUser(user, u => u.AddPermission(permission));

            await dbContext.SaveChangesAsync(cancellationToken);

            if (!hadPermission)
                _logger.Here().Information("Added permission {Permission} to user {UserId}", permission.Value, user.Id);

            return user.ToUserDto(caller);
        }
    }
}
