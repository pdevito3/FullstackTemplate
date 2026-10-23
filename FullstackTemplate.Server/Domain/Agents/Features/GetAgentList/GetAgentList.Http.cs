namespace FullstackTemplate.Server.Domain.Agents.Features;

using System.ComponentModel.DataAnnotations;
using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources;
using Resources.Entrypoints.Http;

public sealed class GetAgentListEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/agents", Handle)
        .WithName("GetAgentList")
        .WithTags("Agents")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem();

    /// <summary>
    /// Gets a paginated list of Agents.
    /// </summary>
    public static async Task<Ok<PagedList<AgentDto>>> Handle(
        string? filters,
        string? sortOrder,
        [Range(1, BasePaginationParameters.MaxPageNumber)] int? pageNumber,
        [Range(1, int.MaxValue)] int? pageSize,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var parameters = AgentParametersDto.From(pageNumber, pageSize, filters, sortOrder);

        var result = await mediator.Send(new GetAgentList.Query(parameters), cancellationToken);
        return TypedResults.Ok(result);
    }
}
