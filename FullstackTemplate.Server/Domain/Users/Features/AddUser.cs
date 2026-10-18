namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;

public static class AddUser
{
    public sealed record Command(UserForCreationDto Dto) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, UserDto>
    {
        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            // The tenant ID comes from the request body, so the caller must be in this tenant.
            // A caller with no user record has no tenant and cannot add a user to any tenant.
            if (!dbContext.CurrentTenantScope.Includes(request.Dto.TenantId))
                throw new ForbiddenAccessException("You can add a user only to your own tenant.");

            var forCreation = request.Dto.ToUserForCreation();
            var user = User.Create(forCreation);

            await dbContext.Users.AddAsync(user, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            
            // TODO handle IDP creation

            return user.ToUserDto();
        }
    }
}
