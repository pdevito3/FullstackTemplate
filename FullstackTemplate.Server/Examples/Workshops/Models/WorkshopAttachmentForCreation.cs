namespace FullstackTemplate.Server.Examples.Workshops.Models;

public sealed record WorkshopAttachmentForCreation
{
    /// <summary>A fixed ID with the prefix of the entity. When it is null, the entity makes a new ID.</summary>
    public string? Id { get; init; }
    public string FileName { get; init; } = default!;
    public string? ContentType { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset UploadedAt { get; init; }
}
