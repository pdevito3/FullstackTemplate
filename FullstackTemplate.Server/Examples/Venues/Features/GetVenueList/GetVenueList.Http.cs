namespace FullstackTemplate.Server.Examples.Venues.Features;

using System.ComponentModel.DataAnnotations;
using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources;
using Resources.Entrypoints.Http;

public sealed class GetVenueListEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/venues", Handle)
        .WithName("GetVenueList")
        .WithTags("Venues")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem();

    /// <summary>
    /// Gets a paginated list of Venues. The search finds the venues whose name contains the text.
    /// </summary>
    public static async Task<Ok<PagedList<VenueDto>>> Handle(
        string? filters,
        string? sortOrder,
        [MaxLength(VenueParametersDto.SearchMaxLength)] string? search,
        [Range(1, BasePaginationParameters.MaxPageNumber)] int? pageNumber,
        [Range(1, int.MaxValue)] int? pageSize,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var parameters = VenueParametersDto.From(pageNumber, pageSize, filters, sortOrder, search);

        var result = await mediator.Send(new GetVenueList.Query(parameters), cancellationToken);
        return TypedResults.Ok(result);
    }
}
