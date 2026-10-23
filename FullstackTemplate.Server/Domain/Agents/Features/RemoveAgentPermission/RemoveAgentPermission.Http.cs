namespace FullstackTemplate.Server.Domain.Agents.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class RemoveAgentPermissionEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapDelete("/agents/{id}/permissions/{permission}", Handle)
        .WithName("RemoveAgentPermission")
        .WithTags("Agents")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

    /// <summary>
    /// Removes a permission from the grant of an Agent. The caller must hold the permission.
    /// </summary>
    public static async Task<Ok<AgentDto>> Handle(
        string id,
        string permission,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RemoveAgentPermission.Command(id, permission), cancellationToken);
        return TypedResults.Ok(result);
    }
}
