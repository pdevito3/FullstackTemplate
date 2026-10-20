namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class GetTenantEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/tenants/{id}", Handle)
        .WithName("GetTenant")
        .WithTags("Tenants")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status404NotFound);

    /// <summary>
    /// Gets a single Tenant by ID.
    /// </summary>
    public static async Task<Ok<TenantDto>> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTenant.Query(id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
