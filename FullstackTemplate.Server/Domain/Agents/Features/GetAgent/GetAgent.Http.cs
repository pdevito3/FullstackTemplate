namespace FullstackTemplate.Server.Domain.Agents.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class GetAgentEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/agents/{id}", Handle)
        .WithName("GetAgent")
        .WithTags("Agents")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

    /// <summary>
    /// Gets a single Agent by ID.
    /// </summary>
    public static async Task<Ok<AgentDto>> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAgent.Query(id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
