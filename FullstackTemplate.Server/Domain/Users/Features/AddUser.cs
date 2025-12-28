namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;

public static class AddUser
{
    public sealed record Command(UserForCreationDto Dto) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, UserDto>
    {
        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var forCreation = request.Dto.ToUserForCreation();
            var user = User.Create(forCreation);

            await dbContext.Users.AddAsync(user, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            
            // TODO handle IDP creation

            return user.ToUserDto();
        }
    }
}
