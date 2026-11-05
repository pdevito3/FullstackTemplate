namespace FullstackTemplate.Server.Domain.Invites.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class GetInvitePreviewEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/invites/preview", Handle)
        .WithName("GetInvitePreview")
        .WithTags("Invites")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status410Gone)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

    /// <summary>
    /// Gets the tenant and the role of an invite, so the join page can show them before the claim. A POST keeps the
    /// token out of the logs of the path and the query.
    /// </summary>
    public static async Task<Ok<InvitePreviewDto>> Handle(
        InviteTokenDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetInvitePreview.Query(dto), cancellationToken);
        return TypedResults.Ok(result);
    }
}
