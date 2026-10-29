namespace FullstackTemplate.Server.Examples.Venues.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class GetVenueEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/venues/{id}", Handle)
        .WithName("GetVenue")
        .WithTags("Venues")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status404NotFound);

    /// <summary>
    /// Gets a single Venue by ID.
    /// </summary>
    public static async Task<Ok<VenueDto>> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetVenue.Query(id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
