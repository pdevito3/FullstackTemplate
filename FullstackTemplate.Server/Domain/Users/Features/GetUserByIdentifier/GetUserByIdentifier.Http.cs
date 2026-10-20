namespace FullstackTemplate.Server.Domain.Users.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class GetUserByIdentifierEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/users/by-identifier/{identifier}", Handle)
        .WithName("GetUserByIdentifier")
        .WithTags("Users")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status404NotFound);

    /// <summary>
    /// Gets a User by their IDP identifier (sub claim).
    /// </summary>
    public static async Task<Ok<UserDto>> Handle(
        string identifier,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserByIdentifier.Query(identifier), cancellationToken);
        return TypedResults.Ok(result);
    }
}
