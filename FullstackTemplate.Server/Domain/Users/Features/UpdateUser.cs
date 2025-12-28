namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;

public static class UpdateUser
{
    public sealed record Command(Guid Id, UserForUpdateDto Dto) : IRequest<UserDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, UserDto>
    {
        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await dbContext.Users.GetById(request.Id, cancellationToken);

            var forUpdate = request.Dto.ToUserForUpdate();
            user.Update(forUpdate);
            
            // TODO handle IDP update

            await dbContext.SaveChangesAsync(cancellationToken);

            return user.ToUserDto();
        }
    }
}
