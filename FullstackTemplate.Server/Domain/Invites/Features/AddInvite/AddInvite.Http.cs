namespace FullstackTemplate.Server.Domain.Invites.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class AddInviteEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/invites", Handle)
        .WithName("AddInvite")
        .WithTags("Invites")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

    /// <summary>
    /// Adds an invite to a tenant. The response has the token one time only.
    /// </summary>
    // No endpoint reads one invite, so the response has no Location
    public static async Task<Created<AddInviteResponseDto>> Handle(
        InviteForCreationDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AddInvite.Command(dto), cancellationToken);
        return TypedResults.Created((string?)null, result);
    }
}
