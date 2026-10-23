namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class DeleteUser
{
    public sealed record Command(string Id) : IRequest;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command>
    {
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ManageUsers());

            var user = await dbContext.Users
                .TagWithOperationCallSite("users.delete.load")
                .Include(u => u.UserPermissions)
                .GetById(request.Id, cancellationToken);
            // A delete takes away each permission of the user.
            caller.Require(user.UserPermissions.Select(p => p.Permission));

            // Remove permissions first (due to PropertyAccessMode.Field)
            foreach (var permission in user.UserPermissions.ToList())
            {
                dbContext.UserPermissions.Remove(permission);
            }

            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
