namespace FullstackTemplate.Server.Examples.Workshops.Models;

using Domain.Attachments;

public sealed record WorkshopAttachmentForCreation
{
    /// <summary>A fixed ID with the prefix of the entity. When it is null, the entity makes a new ID.</summary>
    public string? Id { get; init; }
    public FileUpload File { get; init; } = default!;
    public DateTimeOffset UploadedAt { get; init; }
}
