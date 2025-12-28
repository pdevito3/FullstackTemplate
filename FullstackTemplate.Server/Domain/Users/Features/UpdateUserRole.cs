namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class UpdateUserRole
{
    public sealed record Command(Guid Id, string Role) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, UserDto>
    {
        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await dbContext.Users
                .Include(u => u.UserPermissions)
                .GetById(request.Id, cancellationToken);

            var existingPermissions = user.UserPermissions.ToList();
            var existingPermissionIds = existingPermissions.Select(p => p.Id).ToHashSet();

            var newRole = UserRole.Of(request.Role);
            user.UpdateRole(newRole);

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

            await dbContext.SaveChangesAsync(cancellationToken);

            return user.ToUserDto();
        }
    }
}
