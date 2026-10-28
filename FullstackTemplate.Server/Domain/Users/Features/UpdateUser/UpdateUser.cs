namespace FullstackTemplate.Server.Domain.Users.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;

public static class UpdateUser
{
    public sealed record Command(string Id, UserForUpdateDto Dto) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command, UserDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.user_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            UserAction.Update().GuardBeforeLoad(caller, request.Id);

            var user = await dbContext.Users
                .TagWithOperationCallSite("users.update.load")
                .Include(u => u.UserPermissions)
                .GetById(request.Id, cancellationToken);

            user.GuardAllows(UserAction.Update(), caller);

            var forUpdate = request.Dto.ToUserForUpdate();
            user.Update(forUpdate);
            
            // TODO handle IDP update

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("Updated user {UserId}", user.Id);

            return user.ToUserDto(caller);
        }
    }
}
