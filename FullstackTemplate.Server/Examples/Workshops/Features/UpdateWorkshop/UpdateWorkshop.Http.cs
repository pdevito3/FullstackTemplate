namespace FullstackTemplate.Server.Examples.Workshops.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class UpdateWorkshopEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPut("/workshops/{id}", Handle)
        .WithName("UpdateWorkshop")
        .WithTags("Workshops")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Updates a draft Workshop.
    /// </summary>
    public static async Task<Ok<WorkshopDto>> Handle(
        string id,
        WorkshopForUpdateDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateWorkshop.Command(id, dto), cancellationToken);
        return TypedResults.Ok(result);
    }
}
