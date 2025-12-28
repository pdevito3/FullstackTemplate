namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class RemoveUserPermission
{
    public sealed record Command(Guid Id, string Permission) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, UserDto>
    {
        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await dbContext.Users
                .Include(u => u.UserPermissions)
                .GetById(request.Id, cancellationToken);

            var existingPermissions = user.UserPermissions.ToList();

            var permission = Permission.Of(request.Permission);
            user.RemovePermission(permission);

            // Explicitly remove permissions that were removed from the collection
            var remainingIds = user.UserPermissions.Select(p => p.Id).ToHashSet();
            foreach (var existingPermission in existingPermissions)
            {
                if (!remainingIds.Contains(existingPermission.Id))
                {
                    dbContext.UserPermissions.Remove(existingPermission);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return user.ToUserDto();
        }
    }
}
