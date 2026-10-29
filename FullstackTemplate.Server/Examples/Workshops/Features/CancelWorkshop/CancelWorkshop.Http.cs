namespace FullstackTemplate.Server.Examples.Workshops.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class CancelWorkshopEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/workshops/{id}/cancel", Handle)
        .WithName("CancelWorkshop")
        .WithTags("Workshops")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Cancels a draft or scheduled Workshop.
    /// </summary>
    public static async Task<Ok<WorkshopDto>> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CancelWorkshop.Command(id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
