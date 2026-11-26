namespace FullstackTemplate.Server.Domain.Users.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class UpdateUserRolesEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPut("/users/{id}/roles", Handle)
        .WithName("UpdateUserRoles")
        .WithTags("Users")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Gives a User exactly these roles. The User holds the union of their permissions. A permission that someone
    /// added to or removed from the User by hand stays added or removed.
    /// </summary>
    public static async Task<Ok<UserDto>> Handle(
        string id,
        UpdateUserRolesDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateUserRoles.Command(id, dto.Roles), cancellationToken);
        return TypedResults.Ok(result);
    }
}
