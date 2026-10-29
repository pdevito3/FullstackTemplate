namespace FullstackTemplate.Server.Examples.Venues.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class AddVenueEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/venues", Handle)
        .WithName("AddVenue")
        .WithTags("Venues")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

    /// <summary>
    /// Creates a new Venue.
    /// </summary>
    public static async Task<CreatedAtRoute<VenueDto>> Handle(
        VenueForCreationDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AddVenue.Command(dto), cancellationToken);
        return TypedResults.CreatedAtRoute(result, "GetVenue", new { id = result.Id });
    }
}
