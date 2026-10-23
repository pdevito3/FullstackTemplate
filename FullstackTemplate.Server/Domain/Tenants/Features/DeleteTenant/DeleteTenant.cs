namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Databases;
using MediatR;
using Users;

public static class DeleteTenant
{
    public sealed record Command(string Id) : IRequest;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command>
    {
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            await permissionGuard.RequireAsync(Permission.ManageTenant(), cancellationToken);

            var tenant = await dbContext.Tenants
                .TagWithOperationCallSite("tenants.delete.load")
                .GetById(request.Id, cancellationToken);

            dbContext.Tenants.Remove(tenant);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
