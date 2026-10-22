namespace FullstackTemplate.Server.Domain.Users.Features;

using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class DeleteUserEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapDelete("/users/{id}", Handle)
        .WithName("DeleteUser")
        .WithTags("Users")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Deletes a User.
    /// </summary>
    public static async Task<NoContent> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteUser.Command(id), cancellationToken);
        return TypedResults.NoContent();
    }
}
