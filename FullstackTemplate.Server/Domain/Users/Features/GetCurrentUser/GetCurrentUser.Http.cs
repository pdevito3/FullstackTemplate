namespace FullstackTemplate.Server.Domain.Users.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class GetCurrentUserEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/users/me", Handle)
        .WithName("GetCurrentUser")
        .WithTags("Users")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden);

    /// <summary>
    /// Gets the signed-in caller from the access token. The caller does not need a user record.
    /// </summary>
    public static async Task<Ok<CurrentUserDto>> Handle(IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCurrentUser.Query(), cancellationToken);
        return TypedResults.Ok(result);
    }
}
