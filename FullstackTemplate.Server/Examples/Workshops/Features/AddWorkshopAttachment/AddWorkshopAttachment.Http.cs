namespace FullstackTemplate.Server.Examples.Workshops.Features;

using Dtos;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Resources.Entrypoints.Http;

public sealed class AddWorkshopAttachmentEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/workshops/{id}/attachments", Handle)
        .WithName("AddWorkshopAttachment")
        .WithTags("Workshops")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        // The API takes a bearer token, not a cookie, so a form post of another site cannot send it. The BFF
        // checks its X-CSRF header for the cookie of the SPA.
        .DisableAntiforgery()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

    /// <summary>
    /// Uploads one file to a Workshop as a multipart form with the field file. A file can be no larger than 10 MB.
    /// </summary>
    public static async Task<CreatedAtRoute<WorkshopAttachmentDto>> Handle(
        string id,
        IFormFile file,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var result = await mediator.Send(
            new AddWorkshopAttachment.Command(id, content, file.FileName, file.ContentType, file.Length),
            cancellationToken);
        return TypedResults.CreatedAtRoute(result, "GetWorkshopAttachments", new { id });
    }
}
