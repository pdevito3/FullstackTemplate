namespace FullstackTemplate.Server.Domain.Attachments;

using System.Net.Http.Headers;
using Exceptions;

/// <summary>
/// The files that one use of attachments takes: the allowed types and the largest size. Each entity with attachments
/// declares its own rules. The messages and the list response take the limits from here, so no limit has a copy.
/// </summary>
public sealed class UploadRules
{
    public const string FileEmptyMessage = "The file is empty. Choose a file that has content.";

    private const long BytesPerMegabyte = 1024 * 1024;

    // The SPA reads the size as a JSON number, and the DTO gives it as an int
    private const int MaxSizeMegabytesLimit = (int)(int.MaxValue / BytesPerMegabyte);

    public UploadRules(int maxSizeMegabytes, params FileType[] fileTypes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSizeMegabytes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxSizeMegabytes, MaxSizeMegabytesLimit);
        if (fileTypes.Length == 0)
            throw new ArgumentException("Allow at least one file type.", nameof(fileTypes));

        MaxSizeMegabytes = maxSizeMegabytes;
        FileTypes = fileTypes.Distinct().ToArray();
        FileTooLargeMessage = $"A file can be no larger than {maxSizeMegabytes} MB. Choose a smaller file.";
        TypeNotAllowedMessage = $"The file type must be {OneOf(FileTypes.Select(t => t.Name).ToList())}. Choose another file.";
    }

    public IReadOnlyList<FileType> FileTypes { get; }

    public int MaxSizeMegabytes { get; }

    public long MaxSizeBytes => MaxSizeMegabytes * BytesPerMegabyte;

    public string FileTooLargeMessage { get; }

    public string TypeNotAllowedMessage { get; }

    /// <summary>
    /// Checks the size and the type of an upload, and gives its type. The first bytes name the type, from the allowed
    /// types only. The extension of the name and the media type that the client sent must agree with it.
    /// </summary>
    public FileType TypeOf(FileUpload upload)
    {
        ValidationException.Must(upload.SizeBytes > 0, "sizeBytes", FileEmptyMessage);
        ValidationException.Must(upload.SizeBytes <= MaxSizeBytes, "sizeBytes", FileTooLargeMessage);

        var type = FileTypes.FirstOrDefault(t => t.StartsFile(upload.Head.Span))
            ?? throw new ValidationException("mediaType", TypeNotAllowedMessage);
        ValidationException.Must(type.NamesFile(upload.FileName), "fileName", type.ExtensionMismatchMessage);
        ValidationException.Must(ClaimAgrees(upload.ClaimedMediaType, type), "mediaType", type.MediaTypeMismatchMessage);
        return type;
    }

    // No type, or the type of plain bytes, is no claim: the browser sends it for an extension that it does not know
    private static bool ClaimAgrees(string? claimedMediaType, FileType type)
        => string.IsNullOrWhiteSpace(claimedMediaType)
           || MediaTypeHeaderValue.TryParse(claimedMediaType, out var claimed)
           && (claimed.MediaType is null or "application/octet-stream" || type.HasMediaType(claimed.MediaType));

    // "PNG", "PNG or PDF", "PNG, GIF, or PDF"
    private static string OneOf(List<string> names) => names.Count switch
    {
        1 => names[0],
        2 => $"{names[0]} or {names[1]}",
        _ => $"{string.Join(", ", names.Take(names.Count - 1))}, or {names[^1]}",
    };
}
