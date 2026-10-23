namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Databases;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Services;
using Users;

public static class DeleteTenant
{
    public sealed record Command(string Id) : IRequest;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard, ITenantIdProvider tenantIdProvider)
        : IRequestHandler<Command>
    {
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            await permissionGuard.RequireAsync(Permission.ManageTenant(), cancellationToken);

            var tenant = await dbContext.Tenants
                .TagWithOperationCallSite("tenants.delete.load")
                .GetById(request.Id, cancellationToken);

            var userIdentifiers = await dbContext.Users
                .TagWithOperationCallSite("tenants.delete.load_user_identifiers")
                .Where(u => u.TenantId == tenant.Id)
                .Select(u => u.Identifier)
                .ToListAsync(cancellationToken);

            dbContext.Tenants.Remove(tenant);
            await dbContext.SaveChangesAsync(cancellationToken);

            // The cache keeps the tenant of each user. Remove it, so the users of this tenant lose access now.
            foreach (var userIdentifier in userIdentifiers)
            {
                await tenantIdProvider.InvalidateCacheAsync(userIdentifier, cancellationToken);
            }
        }
    }
}
