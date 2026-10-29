namespace FullstackTemplate.Server.Domain.Tenants.Features;

using System.Diagnostics;
using Databases;
using MediatR;
using Resources;
using Serilog;
using Users;

public static class DeleteTenant
{
    public sealed record Command(string Id) : IRequest, IRunsAgainWhenRaced;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.tenant_id", request.Id);

            await permissionGuard.RequireAsync(Permission.ManageTenant(), cancellationToken);

            // The exclusive lock makes each add that takes the shared lock of the tenant wait, so no such row comes after
            // the load. A deleted user frees its identifier, so the same person can sign up again.
            var deletedCounts = await RootLock.Tenant.RunExclusiveAsync(dbContext, request.Id, async ct =>
            {
                var tenant = await dbContext.Tenants
                    .TagWithOperationCallSite("tenants.delete.load")
                    .GetById(request.Id, ct);

                var rows = await TenantOwnedRows.LoadAsync(dbContext, tenant.Id, ct);
                foreach (var row in rows)
                    dbContext.Remove(row is IDeletedWithEvent deletedWithEvent ? deletedWithEvent.Delete() : row);

                dbContext.Tenants.Remove(tenant);
                await dbContext.SaveChangesAsync(ct);
                return rows.CountBy(row => row.GetType().Name).ToDictionary();
            }, cancellationToken);

            _logger.Here().Information("Deleted tenant {TenantId} and its rows {@DeletedCounts}", request.Id, deletedCounts);
        }
    }
}
