namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class AddUserPermission
{
    public sealed record Command(Guid Id, string Permission) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, UserDto>
    {
        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await dbContext.Users
                .Include(u => u.UserPermissions)
                .GetById(request.Id, cancellationToken);

            var existingPermissionIds = user.UserPermissions.Select(p => p.Id).ToHashSet();

            var permission = Permission.Of(request.Permission);
            user.AddPermission(permission);

            // Explicitly track new permissions (needed due to PropertyAccessMode.Field)
            foreach (var userPermission in user.UserPermissions)
            {
                if (!existingPermissionIds.Contains(userPermission.Id))
                {
                    dbContext.UserPermissions.Add(userPermission);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return user.ToUserDto();
        }
    }
}
