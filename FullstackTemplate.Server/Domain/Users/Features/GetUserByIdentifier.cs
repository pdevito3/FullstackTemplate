namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class GetUserByIdentifier
{
    public sealed record Query(string Identifier) : IRequest<UserDto?>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, UserDto?>
    {
        public async Task<UserDto?> Handle(Query request, CancellationToken cancellationToken)
        {
            var user = await dbContext.Users
                .AsNoTracking()
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Identifier == request.Identifier, cancellationToken);

            return user?.ToUserDto();
        }
    }
}
