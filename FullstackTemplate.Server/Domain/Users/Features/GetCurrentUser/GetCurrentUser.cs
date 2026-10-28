namespace FullstackTemplate.Server.Domain.Users.Features;

using Dtos;
using Exceptions;
using MediatR;
using Services;

public static class GetCurrentUser
{
    public sealed record Query : IRequest<CurrentUserDto>;

    // The token is the only source, so a user with no user record gets an answer too
    public sealed class Handler(ICurrentUserService currentUserService) : IRequestHandler<Query, CurrentUserDto>
    {
        public Task<CurrentUserDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var identifier = currentUserService.UserIdentifier
                ?? throw new ForbiddenAccessException("Only a signed-in user can do this. The token has no subject.");
            return Task.FromResult(new CurrentUserDto { Identifier = identifier, Name = currentUserService.Name });
        }
    }
}
