namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.ComponentModel.DataAnnotations;
using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources;
using Resources.Entrypoints.Http;

public sealed class GetWorkshopListEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/workshops", Handle)
        .WithName("GetWorkshopList")
        .WithTags("Workshops")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem();

    /// <summary>
    /// Gets a paginated list of Workshops.
    /// </summary>
    public static async Task<Ok<PagedList<WorkshopDto>>> Handle(
        [MaxLength(CustomQueryKitConfiguration.FilterMaxLength)] string? filters,
        string? sortOrder,
        [Range(1, BasePaginationParameters.MaxPageNumber)] int? pageNumber,
        [Range(1, int.MaxValue)] int? pageSize,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var parameters = WorkshopParametersDto.From(pageNumber, pageSize, filters, sortOrder);

        var result = await mediator.Send(new GetWorkshopList.Query(parameters), cancellationToken);
        return TypedResults.Ok(result);
    }
}
