namespace FullstackTemplate.Server.Domain.AuditEntries.Features;

using System.ComponentModel.DataAnnotations;
using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources;
using Resources.Entrypoints.Http;

public sealed class GetAuditEntryPageEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/audit-entries", Handle)
        .WithName("GetAuditEntryPage")
        .WithTags("AuditEntries")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden);

    /// <summary>
    /// Gets one page of audit entries, newest first. With an entityId, it gets the history of that aggregate root and
    /// its children. With no entityId, it gets the entries of the tenant. Needs read_audit_log. A value that the caller
    /// may not read has hidden set and no old or new value. To get the next page, send the nextCursor of this page as
    /// the cursor. The last page has no nextCursor.
    /// </summary>
    public static async Task<Ok<AuditEntryPageDto>> Handle(
        string? entityId,
        string? cursor,
        [Range(1, int.MaxValue)] int? pageSize,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var parameters = AuditEntryParametersDto.From(entityId, cursor, pageSize);

        var result = await mediator.Send(new GetAuditEntryPage.Query(parameters), cancellationToken);
        return TypedResults.Ok(result);
    }
}
