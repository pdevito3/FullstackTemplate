namespace FullstackTemplate.Server.Resources.Storage;

/// <summary>
/// A presigned GET of one object. S3 sends the response with this content type and this content disposition, not
/// with the values that it stored, so the server decides how the browser treats the bytes.
/// </summary>
public sealed record PresignedGet
{
    public required string Key { get; init; }
    public required string ContentType { get; init; }
    public required string ContentDisposition { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>A GET that the browser saves as a file with the name <paramref name="fileName"/>.</summary>
    public static PresignedGet Attachment(string key, string fileName, string contentType, DateTimeOffset expiresAt)
        => new()
        {
            Key = key,
            ContentType = contentType,
            ContentDisposition = $"attachment; {FileNameParameter(fileName)}",
            ExpiresAt = expiresAt,
        };

    /// <summary>A GET that the browser shows in the page, for example in an <c>img</c> element.</summary>
    public static PresignedGet Inline(string key, string fileName, string contentType, DateTimeOffset expiresAt)
        => new()
        {
            Key = key,
            ContentType = contentType,
            ContentDisposition = $"inline; {FileNameParameter(fileName)}",
            ExpiresAt = expiresAt,
        };

    // RFC 6266 with the RFC 8187 encoding. The percent encoding keeps only letters, digits, and "-._~", which are all
    // attr-chars, so a quote, a semicolon, a CR, or an LF in the name cannot end the parameter or the header.
    private static string FileNameParameter(string fileName) => $"filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
}
