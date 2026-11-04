namespace FullstackTemplate.Server.Domain.Invites.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;
using Users.Dtos;

public sealed class ClaimInviteEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/invites/claim", Handle)
        .WithName("ClaimInvite")
        .WithTags("Invites")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status410Gone)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Claims an invite: adds the caller to the tenant of the invite as a new user with the role of the invite.
    /// </summary>
    public static async Task<CreatedAtRoute<UserDto>> Handle(
        ClaimInviteDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ClaimInvite.Command(dto), cancellationToken);
        return TypedResults.CreatedAtRoute(result, "GetUser", new { id = result.Id });
    }
}
