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

public static class UpdateUserRole
{
    // The endpoint sends the role of UpdateUserRoleDto, so the rule of the DTO is also on the command
    public sealed record Command(string Id, [property: RequiredField] string Role) : IRequest<UserDto>, IRunsAgainWhenRaced;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command, UserDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.user_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            UserAction.UpdateRole().GuardBeforeLoad(caller, request.Id);
            var newRole = UserRole.Of(request.Role);
            Activity.Current?.SetTag("app.user.role", newRole.Value);
            caller.Require(newRole.GetDefaultPermissions());

            var tenantId = (await dbContext.Users
                .TagWithOperationCallSite("users.update_role.find_tenant")
                .AsNoTracking()
                .GetById(request.Id, cancellationToken)).TenantId;

            var (user, oldRole) = await RootLock.Tenant.RunExclusiveAsync(dbContext, tenantId, async ct =>
            {
                var user = await dbContext.Users
                    .TagWithOperationCallSite("users.update_role.load")
                    .Include(u => u.UserPermissions)
                    .GetById(request.Id, ct);
                user.GuardAllows(UserAction.UpdateRole(), caller);

                var admins = await dbContext.LoadAdminCaseAsync(user, new AdminChange.NewRole(newRole), ct);
                var kept = TenantAdmins.Decide(admins) switch
                {
                    AdminDecision.Kept k => k.Proof,
                    AdminDecision.Refused { Refusal: AdminRefusal.LastAdmin lastAdmin } => throw Refuse(lastAdmin),
                    _ => throw new UnreachableException(),
                };

                var existingPermissions = user.UserPermissions.ToList();
                var existingPermissionIds = existingPermissions.Select(p => p.Id).ToHashSet();

                var oldRole = user.Role;
                user.UpdateRole(kept);

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

                await dbContext.SaveChangesAsync(ct);
                return (user, oldRole);
            }, cancellationToken);

            if (newRole != oldRole)
            {
                _logger.Here().Information("Changed the role of user {UserId} to {Role}", user.Id, newRole.Value);
                UserMetrics.RoleChanged.Add(1, new KeyValuePair<string, object?>("app.user.role", newRole.Value));
            }

            return user.ToUserDto(caller);
        }

        private static ValidationException Refuse(AdminRefusal refusal)
        {
            Activity.Current?.SetTag("app.user.admin_refusal", "last_admin");
            return new ValidationException(refusal.Field, refusal.Message);
        }
    }
}
