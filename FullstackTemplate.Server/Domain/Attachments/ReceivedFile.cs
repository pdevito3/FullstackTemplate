namespace FullstackTemplate.Server.Domain.Attachments;

/// <summary>
/// The file of an upload request: its bytes in a stream that can seek, its name, and the media type that the client
/// claims. The entrypoint makes it from the request and disposes it. <see cref="FileUpload.ReadAsync"/> reads it.
/// </summary>
public sealed class ReceivedFile : IAsyncDisposable
{
    public ReceivedFile(Stream content, string fileName, string? claimedMediaType)
    {
        if (!content.CanSeek)
            throw new ArgumentException("The content must be a stream that can seek, so that the put sends the first bytes again.", nameof(content));

        Content = content;
        FileName = fileName;
        ClaimedMediaType = claimedMediaType;
    }

    /// <summary>All of the bytes of the file, from position 0 to the end.</summary>
    public Stream Content { get; }
    public string FileName { get; }
    public string? ClaimedMediaType { get; }
    public long SizeBytes => Content.Length;

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
