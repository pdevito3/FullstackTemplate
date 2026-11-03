namespace FullstackTemplate.Server.Domain.Attachments.Dtos;

public sealed record AttachmentDto
{
    public string Id { get; init; } = default!;
    public string FileName { get; init; } = default!;
    /// <summary>The media type that the first bytes of the file named, for example image/png.</summary>
    public string MediaType { get; init; } = default!;
    // Not a long: Kubb types an int64 as a bigint, but the SPA reads a JSON number
    public int SizeBytes { get; init; }
    public DateTimeOffset UploadedAt { get; init; }
    /// <summary>A presigned URL of object storage. The browser saves the file under its name.</summary>
    public string DownloadUrl { get; init; } = default!;
    /// <summary>A presigned URL that an img element can show. Null when the type of the file cannot show in the page.</summary>
    public string? PreviewUrl { get; init; }
    /// <summary>The time after which the URLs stop working. Load the list again before it.</summary>
    public DateTimeOffset UrlsExpireAt { get; init; }
}
