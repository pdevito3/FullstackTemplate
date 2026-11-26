namespace FullstackTemplate.Server.Domain.Users.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;

public static class UpdateUserRoles
{
    // The endpoint sends the roles of UpdateUserRolesDto, so the rule of the DTO is also on the command
    public sealed record Command(string Id, [property: RequiredField] IReadOnlyList<string> Roles)
        : IRequest<UserDto>, IRunsAgainWhenRaced;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command, UserDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.user_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            UserAction.UpdateRoles().GuardBeforeLoad(caller, request.Id);
            var newRoles = request.Roles.Select((r, i) => UserRole.Of(r, $"roles[{i}]")).ToHashSet();
            Activity.Current?.SetTag("app.user.roles", string.Join(",", newRoles.Select(r => r.Value).Order()));

            var tenantId = (await dbContext.Users
                .TagWithOperationCallSite("users.update_roles.find_tenant")
                .AsNoTracking()
                .GetById(request.Id, cancellationToken)).TenantId;

            var (user, added, removed) = await RootLock.Tenant.RunExclusiveAsync(dbContext, tenantId, async ct =>
            {
                var user = await dbContext.Users
                    .TagWithOperationCallSite("users.update_roles.load")
                    .Include(u => u.UserRoles)
                    .Include(u => u.PermissionOverrides)
                    .Include(u => u.UserPermissions)
                    .GetById(request.Id, ct);
                user.GuardAllows(UserAction.UpdateRoles(), caller);

                // The caller grants each permission of a role that the user gets now, so nobody grants more than
                // they hold. A role that the user keeps grants nothing new.
                var added = newRoles.Except(user.CurrentRoles()).ToList();
                var removed = user.CurrentRoles().Except(newRoles).ToList();
                caller.Require(UserRole.PermissionsOf(added).ToArray());

                var kept = await dbContext.KeepManagerAsync(user, new ManagerChange.NewRoles(newRoles), ct);

                dbContext.ChangeUser(user, u => u.UpdateRoles(kept));

                await dbContext.SaveChangesAsync(ct);
                return (user, added, removed);
            }, cancellationToken);

            foreach (var role in added)
            {
                _logger.Here().Information("Gave role {Role} to user {UserId}", role.Value, user.Id);
                UserMetrics.RoleChanged.Add(1, new KeyValuePair<string, object?>("app.user.role", role.Value));
            }

            foreach (var role in removed)
                _logger.Here().Information("Took role {Role} from user {UserId}", role.Value, user.Id);

            return user.ToUserDto(caller);
        }
    }
}
