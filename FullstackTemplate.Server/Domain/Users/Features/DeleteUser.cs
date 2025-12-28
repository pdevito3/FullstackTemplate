namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class DeleteUser
{
    public sealed record Command(Guid Id) : IRequest;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command>
    {
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await dbContext.Users
                .Include(u => u.UserPermissions)
                .GetById(request.Id, cancellationToken);

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
