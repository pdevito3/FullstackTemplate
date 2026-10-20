namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class AddTenantEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/tenants", Handle)
        .WithName("AddTenant")
        .WithTags("Tenants")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Creates a new Tenant.
    /// </summary>
    public static async Task<CreatedAtRoute<TenantDto>> Handle(
        TenantForCreationDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AddTenant.Command(dto), cancellationToken);
        return TypedResults.CreatedAtRoute(result, "GetTenant", new { id = result.Id });
    }
}
