namespace FullstackTemplate.Server.Domain.Invites.Features;

using System.ComponentModel.DataAnnotations;
using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources;
using Resources.Entrypoints.Http;

public sealed class GetInviteListEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/invites", Handle)
        .WithName("GetInviteList")
        .WithTags("Invites")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden);

    /// <summary>
    /// Gets a page of the invites of the tenant.
    /// </summary>
    public static async Task<Ok<PagedList<InviteDto>>> Handle(
        [MaxLength(CustomQueryKitConfiguration.FilterMaxLength)] string? filters,
        string? sortOrder,
        [Range(1, BasePaginationParameters.MaxPageNumber)] int? pageNumber,
        [Range(1, int.MaxValue)] int? pageSize,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var parameters = InviteParametersDto.From(pageNumber, pageSize, filters, sortOrder);

        var result = await mediator.Send(new GetInviteList.Query(parameters), cancellationToken);
        return TypedResults.Ok(result);
    }
}
