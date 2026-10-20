namespace FullstackTemplate.Server.Domain.Users.Features;

using System.ComponentModel.DataAnnotations;
using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources;
using Resources.Entrypoints.Http;

public sealed class GetUserListEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/users", Handle)
        .WithName("GetUserList")
        .WithTags("Users")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem();

    /// <summary>
    /// Gets a paginated list of Users.
    /// </summary>
    public static async Task<Ok<PagedList<UserDto>>> Handle(
        string? filters,
        string? sortOrder,
        [Range(1, BasePaginationParameters.MaxPageNumber)] int? pageNumber,
        [Range(1, int.MaxValue)] int? pageSize,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var parameters = UserParametersDto.From(pageNumber, pageSize, filters, sortOrder);

        var result = await mediator.Send(new GetUserList.Query(parameters), cancellationToken);
        return TypedResults.Ok(result);
    }
}
