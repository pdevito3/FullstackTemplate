namespace FullstackTemplate.Server.Domain.Agents.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class AddAgentEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/agents", Handle)
        .WithName("AddAgent")
        .WithTags("Agents")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Registers an OAuth client as an agent of a tenant, with its first grant.
    /// </summary>
    public static async Task<CreatedAtRoute<AgentDto>> Handle(
        AgentForCreationDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AddAgent.Command(dto), cancellationToken);
        return TypedResults.CreatedAtRoute(result, "GetAgent", new { id = result.Id });
    }
}
