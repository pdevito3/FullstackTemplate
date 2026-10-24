namespace FullstackTemplate.Server.Domain.Users.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class GetUserByIdentifier
{
    public sealed record Query(string Identifier) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, UserDto>
    {
        public async Task<UserDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var user = await dbContext.Users
                .TagWithOperationCallSite("users.get_by_identifier")
                .AsNoTracking()
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Identifier == request.Identifier, cancellationToken);
            Activity.Current?.SetTag("app.user_id", user?.Id);

            return user?.ToUserDto() ?? throw new NotFoundException("User was not found.");
        }
    }
}
