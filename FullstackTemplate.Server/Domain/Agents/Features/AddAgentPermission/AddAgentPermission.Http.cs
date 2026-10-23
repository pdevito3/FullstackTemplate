namespace FullstackTemplate.Server.Domain.Agents.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class AddAgentPermissionEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/agents/{id}/permissions", Handle)
        .WithName("AddAgentPermission")
        .WithTags("Agents")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

    /// <summary>
    /// Adds a permission to the grant of an Agent. The caller must hold the permission.
    /// </summary>
    public static async Task<Ok<AgentDto>> Handle(
        string id,
        AgentPermissionDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AddAgentPermission.Command(id, dto.Permission), cancellationToken);
        return TypedResults.Ok(result);
    }
}
