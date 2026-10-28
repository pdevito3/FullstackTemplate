namespace FullstackTemplate.Server.Domain.Users.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class GetUser
{
    public sealed record Query(string Id) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Query, UserDto>
    {
        public async Task<UserDto> Handle(Query request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.user_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.RequireSelfOr(request.Id, Permission.ReadUsers());

            var user = await dbContext.Users
                .TagWithOperationCallSite("users.get_by_id")
                .Include(u => u.UserPermissions)
                .GetById(request.Id, cancellationToken);
            return user.ToUserDto(caller);
        }
    }
}
