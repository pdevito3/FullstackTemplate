namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;

public static class UpdateUser
{
    // A user can update their own record. The role is not in the DTO, so this cannot make a user an admin.
    public sealed record Command(Guid Id, UserForUpdateDto Dto) : IRequest<UserDto>, IRequireTenantAdmin, IAllowSelf
    {
        public Guid SelfUserId => Id;
    }

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, UserDto>
    {
        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await dbContext.Users
                .TagWithOperationCallSite("users.update.load")
                .GetById(request.Id, cancellationToken);

            var forUpdate = request.Dto.ToUserForUpdate();
            user.Update(forUpdate);
            
            // TODO handle IDP update

            await dbContext.SaveChangesAsync(cancellationToken);

            return user.ToUserDto();
        }
    }
}
