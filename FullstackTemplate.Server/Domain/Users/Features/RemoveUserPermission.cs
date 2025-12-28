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

            var permission = Permission.Of(request.Permission);
            user.RemovePermission(permission);

            await dbContext.SaveChangesAsync(cancellationToken);

            return user.ToUserDto();
        }
    }
}
