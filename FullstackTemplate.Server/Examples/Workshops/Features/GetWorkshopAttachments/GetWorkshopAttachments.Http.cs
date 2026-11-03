namespace FullstackTemplate.Server.Examples.Workshops.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class GetWorkshopAttachmentsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapGet("/workshops/{id}/attachments", Handle)
        .WithName("GetWorkshopAttachments")
        .WithTags("Workshops")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status404NotFound);

    /// <summary>
    /// Gets the attachments of a Workshop, oldest first, with presigned URLs that expire at urlsExpireAt.
    /// </summary>
    public static async Task<Ok<List<WorkshopAttachmentDto>>> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWorkshopAttachments.Query(id), cancellationToken);
        return TypedResults.Ok(result);
    }
}
