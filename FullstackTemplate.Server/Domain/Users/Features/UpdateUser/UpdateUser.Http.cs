namespace FullstackTemplate.Server.Domain.Users.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class UpdateUserEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPut("/users/{id}", Handle)
        .WithName("UpdateUser")
        .WithTags("Users")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Updates an existing User.
    /// </summary>
    public static async Task<Ok<UserDto>> Handle(
        string id,
        UserForUpdateDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateUser.Command(id, dto), cancellationToken);
        return TypedResults.Ok(result);
    }
}
