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
    public sealed record Command(string Id, string Permission) : IRequest<UserDto>, IRunsAgainWhenRaced;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command, UserDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.user_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            UserAction.RemovePermission().GuardBeforeLoad(caller, request.Id);
            var permission = Permission.Of(request.Permission);
            Activity.Current?.SetTag("app.permission.name", permission.Value);
            caller.Require(permission);

            var tenantId = (await dbContext.Users
                .TagWithOperationCallSite("users.remove_permission.find_tenant")
                .AsNoTracking()
                .GetById(request.Id, cancellationToken)).TenantId;

            var (user, hadPermission) = await RootLock.Tenant.RunExclusiveAsync(dbContext, tenantId, async ct =>
            {
                var user = await dbContext.Users
                    .TagWithOperationCallSite("users.remove_permission.load")
                    .Include(u => u.UserRoles)
                    .Include(u => u.PermissionOverrides)
                    .Include(u => u.UserPermissions)
                    .GetById(request.Id, ct);
                user.GuardAllows(UserAction.RemovePermission(), caller);

                var kept = await dbContext.KeepManagerAsync(user, new ManagerChange.PermissionRemoval(permission), ct);

                var hadPermission = user.HasPermission(permission);
                dbContext.ChangeUser(user, u => u.RemovePermission(kept));

                await dbContext.SaveChangesAsync(ct);
                return (user, hadPermission);
            }, cancellationToken);

            if (hadPermission)
                _logger.Here().Information("Removed permission {Permission} from user {UserId}", permission.Value, user.Id);

            return user.ToUserDto(caller);
        }
    }
}
