namespace FullstackTemplate.Server.Examples.Workshops.Dtos;

public sealed record WorkshopAttachmentDto
{
    public string Id { get; init; } = default!;
    public string FileName { get; init; } = default!;
    /// <summary>The type that the client sent with the file, for example image/png.</summary>
    public string ContentType { get; init; } = default!;
    public long SizeBytes { get; init; }
    public DateTimeOffset UploadedAt { get; init; }
    /// <summary>A presigned URL of object storage. The browser saves the file under its name.</summary>
    public string DownloadUrl { get; init; } = default!;
    /// <summary>A presigned URL that an img element can show. Null for each type that is not a PNG, JPEG, GIF, or WebP image.</summary>
    public string? PreviewUrl { get; init; }
    /// <summary>The time after which the URLs stop working. Load the list again before it.</summary>
    public DateTimeOffset UrlsExpireAt { get; init; }
}
