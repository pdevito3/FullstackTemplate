namespace FullstackTemplate.Server.Examples.Workshops.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class AddWorkshopEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/workshops", Handle)
        .WithName("AddWorkshop")
        .WithTags("Workshops")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

    /// <summary>
    /// Creates a new Workshop as a draft.
    /// </summary>
    public static async Task<CreatedAtRoute<WorkshopDto>> Handle(
        WorkshopForCreationDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AddWorkshop.Command(dto), cancellationToken);
        return TypedResults.CreatedAtRoute(result, "GetWorkshop", new { id = result.Id });
    }
}
