namespace FullstackTemplate.Server.Domain.Invites.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class RevokeInviteEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/invites/{id}/revoke", Handle)
        .WithName("RevokeInvite")
        .WithTags("Invites")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Revokes a pending invite, so that no one can claim it.
    /// </summary>
    public static async Task<Ok<InviteDto>> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RevokeInvite.Command(id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
