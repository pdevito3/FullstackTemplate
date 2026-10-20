namespace FullstackTemplate.Server.Domain.Users.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class InitiateUserAndTenantEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/users/initiate", Handle)
        .WithName("InitiateUserAndTenant")
        .WithTags("Users")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Creates a new Tenant and User together in a single transaction.
    /// Use this to onboard a new organization with its first admin user.
    /// </summary>
    public static async Task<CreatedAtRoute<InitiateUserAndTenantResponseDto>> Handle(
        InitiateUserAndTenantDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new InitiateUserAndTenant.Command(dto), cancellationToken);
        return TypedResults.CreatedAtRoute(result, "GetUser", new { id = result.User.Id });
    }
}
