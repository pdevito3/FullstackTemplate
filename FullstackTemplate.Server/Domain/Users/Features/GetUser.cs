namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class GetUser
{
    public sealed record Query(Guid Id) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, UserDto>
    {
        public async Task<UserDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var user = await dbContext.Users
                .Include(u => u.UserPermissions)
                .GetById(request.Id, cancellationToken);
            return user.ToUserDto();
        }
    }
}
