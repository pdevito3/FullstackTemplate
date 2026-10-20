namespace FullstackTemplate.Server.Domain.Tenants.Features;

using System.ComponentModel.DataAnnotations;
using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources;
using Resources.Entrypoints.Http;

public sealed class GetTenantListEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/tenants", Handle)
        .WithName("GetTenantList")
        .WithTags("Tenants")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem();

    /// <summary>
    /// Gets a paginated list of Tenants.
    /// </summary>
    public static async Task<Ok<PagedList<TenantDto>>> Handle(
        string? filters,
        string? sortOrder,
        [Range(1, BasePaginationParameters.MaxPageNumber)] int? pageNumber,
        [Range(1, int.MaxValue)] int? pageSize,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var parameters = TenantParametersDto.From(pageNumber, pageSize, filters, sortOrder);

        var result = await mediator.Send(new GetTenantList.Query(parameters), cancellationToken);
        return TypedResults.Ok(result);
    }
}
