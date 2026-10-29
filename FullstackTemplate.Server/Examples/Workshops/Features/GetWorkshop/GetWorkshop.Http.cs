namespace FullstackTemplate.Server.Examples.Workshops.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class GetWorkshopEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/workshops/{id}", Handle)
        .WithName("GetWorkshop")
        .WithTags("Workshops")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status404NotFound);

    /// <summary>
    /// Gets a single Workshop by ID.
    /// </summary>
    public static async Task<Ok<WorkshopDto>> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWorkshop.Query(id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
