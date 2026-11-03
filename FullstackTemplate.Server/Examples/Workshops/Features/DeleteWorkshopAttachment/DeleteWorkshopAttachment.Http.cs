namespace FullstackTemplate.Server.Examples.Workshops.Features;

using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class DeleteWorkshopAttachmentEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapDelete("/workshops/{id}/attachments/{attachmentId}", Handle)
        .WithName("DeleteWorkshopAttachment")
        .WithTags("Workshops")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// Deletes an attachment of a Workshop and its file.
    /// </summary>
    public static async Task<NoContent> Handle(
        string id,
        string attachmentId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteWorkshopAttachment.Command(id, attachmentId), cancellationToken);
        return TypedResults.NoContent();
    }
}
