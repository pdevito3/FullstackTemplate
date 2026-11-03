namespace FullstackTemplate.Server.Domain.Attachments;

/// <summary>
/// What a client sends with a file: its name, the media type that the client gives, its size, and its first bytes.
/// <see cref="StoredFile.Accept"/> checks it.
/// </summary>
/// <param name="FileName">The name that the client sent with the file.</param>
/// <param name="ClaimedMediaType">The media type that the client sent, if any. The browser names it from the
/// extension, so it is only a claim. The first bytes decide the type.</param>
/// <param name="SizeBytes">The length of the file.</param>
/// <param name="Head">The first bytes of the file, at most <see cref="FileType.HeadLength"/>.</param>
public sealed record FileUpload(string FileName, string? ClaimedMediaType, long SizeBytes, ReadOnlyMemory<byte> Head)
{
    /// <summary>
    /// Reads the first bytes of the content, then puts the stream back at its start, so that the put sends all of the
    /// file from the same stream. Only the first bytes are in memory. The stream must be able to seek, as the stream of
    /// a form file can.
    /// </summary>
    public static async Task<FileUpload> ReadAsync(Stream content, string fileName, string? claimedMediaType,
        long sizeBytes, CancellationToken cancellationToken)
    {
        if (!content.CanSeek)
            throw new ArgumentException("The content must be a stream that can seek, so that the put sends the first bytes again.", nameof(content));

        var start = content.Position;
        var head = new byte[FileType.HeadLength];
        var read = await content.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken);
        content.Position = start;
        return new FileUpload(fileName, claimedMediaType, sizeBytes, head.AsMemory(0, read));
    }
}
