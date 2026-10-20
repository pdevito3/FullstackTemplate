namespace FullstackTemplate.Server.Domain.Users.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class AddUserEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/users", Handle)
        .WithName("AddUser")
        .WithTags("Users")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Creates a new User.
    /// </summary>
    public static async Task<CreatedAtRoute<UserDto>> Handle(
        UserForCreationDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AddUser.Command(dto), cancellationToken);
        return TypedResults.CreatedAtRoute(result, "GetUser", new { id = result.Id });
    }
}
