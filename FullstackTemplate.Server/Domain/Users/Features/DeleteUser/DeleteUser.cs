namespace FullstackTemplate.Server.Domain.Users.Features;

using System.Diagnostics;
using Databases;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;

public static class DeleteUser
{
    public sealed record Command(string Id) : IRequest, IRunsAgainWhenRaced;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.user_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            UserAction.Delete().GuardBeforeLoad(caller, request.Id);

            var tenantId = (await dbContext.Users
                .TagWithOperationCallSite("users.delete.find_tenant")
                .AsNoTracking()
                .GetById(request.Id, cancellationToken)).TenantId;

            var user = await RootLock.Tenant.RunExclusiveAsync(dbContext, tenantId, async ct =>
            {
                var user = await dbContext.Users
                    .TagWithOperationCallSite("users.delete.load")
                    .Include(u => u.UserPermissions)
                    .GetById(request.Id, ct);
                user.GuardAllows(UserAction.Delete(), caller);

                var kept = await dbContext.KeepManagerAsync(user, new ManagerChange.Removal(), ct);

                // Remove permissions first (due to PropertyAccessMode.Field)
                foreach (var permission in user.UserPermissions.ToList())
                {
                    dbContext.UserPermissions.Remove(permission);
                }

                dbContext.Users.Remove(user.Delete(kept));
                await dbContext.SaveChangesAsync(ct);
                return user;
            }, cancellationToken);

            _logger.Here().Information("Deleted user {UserId}", user.Id);
        }
    }
}
