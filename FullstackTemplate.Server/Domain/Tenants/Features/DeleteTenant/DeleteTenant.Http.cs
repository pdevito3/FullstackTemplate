namespace FullstackTemplate.Server.Domain.Tenants.Features;

using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class DeleteTenantEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapDelete("/tenants/{id}", Handle)
        .WithName("DeleteTenant")
        .WithTags("Tenants")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Deletes a Tenant.
    /// </summary>
    public static async Task<NoContent> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTenant.Command(id), cancellationToken);
        return TypedResults.NoContent();
    }
}
