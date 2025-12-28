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

            var newRole = UserRole.Of(request.Role);
            user.UpdateRole(newRole);

            await dbContext.SaveChangesAsync(cancellationToken);

            return user.ToUserDto();
        }
    }
}
