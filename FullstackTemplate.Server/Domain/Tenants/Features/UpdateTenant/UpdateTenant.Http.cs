namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class UpdateTenantEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPut("/tenants/{id}", Handle)
        .WithName("UpdateTenant")
        .WithTags("Tenants")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Updates an existing Tenant.
    /// </summary>
    public static async Task<Ok<TenantDto>> Handle(
        string id,
        TenantForUpdateDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateTenant.Command(id, dto), cancellationToken);
        return TypedResults.Ok(result);
    }
}
