namespace FullstackTemplate.Server.Examples.Workshops.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class ScheduleWorkshopEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/workshops/{id}/schedule", Handle)
        .WithName("ScheduleWorkshop")
        .WithTags("Workshops")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Schedules a draft Workshop. Its start must be on a later day than today and in the opening hours, both in the
    /// time zone of its venue.
    /// </summary>
    public static async Task<Ok<WorkshopDto>> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ScheduleWorkshop.Command(id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
