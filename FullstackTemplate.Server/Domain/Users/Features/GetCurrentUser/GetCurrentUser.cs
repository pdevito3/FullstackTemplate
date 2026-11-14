namespace FullstackTemplate.Server.Domain.Users.Features;

using Dtos;
using Exceptions;
using MediatR;
using Resources.Jobs;

public static class GetCurrentUser
{
    public sealed record Query : IRequest<CurrentUserDto>;

    public sealed class Handler(ICurrentUserService currentUserService, JobOperators jobOperators)
        : IRequestHandler<Query, CurrentUserDto>
    {
        public async Task<CurrentUserDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var identifier = currentUserService.UserIdentifier
                ?? throw new ForbiddenAccessException("Only a signed-in user can do this. The token has no subject.");
            return new CurrentUserDto
            {
                Identifier = identifier,
                Name = currentUserService.Name,
                CanOpenJobDashboard = await jobOperators.IncludesCallerAsync(cancellationToken),
            };
        }
    }
}
