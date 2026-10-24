namespace FullstackTemplate.Server.Domain.Users.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Mappings;
using MediatR;
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

            await permissionGuard.RequireSelfOrAsync(request.Id, Permission.ManageUsers(), cancellationToken);

            var user = await dbContext.Users
                .TagWithOperationCallSite("users.update.load")
                .GetById(request.Id, cancellationToken);

            var forUpdate = request.Dto.ToUserForUpdate();
            user.Update(forUpdate);
            
            // TODO handle IDP update

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("Updated user {UserId}", user.Id);

            return user.ToUserDto();
        }
    }
}
