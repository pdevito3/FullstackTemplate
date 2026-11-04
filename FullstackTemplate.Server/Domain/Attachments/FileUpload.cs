namespace FullstackTemplate.Server.Domain.Attachments;

/// <summary>
/// What a client sends with a file: its name, the media type that the client claims, its size, and its first bytes, at
/// most <see cref="FileType.HeadLength"/>. The browser names the media type from the extension, so it is only a claim.
/// The first bytes decide the type. <see cref="StoredFile.Accept"/> checks it.
/// </summary>
public sealed record FileUpload(string FileName, string? ClaimedMediaType, long SizeBytes, ReadOnlyMemory<byte> Head)
{
    /// <summary>
    /// Reads the first bytes of the file, then puts the stream back at its start, so that the put sends all of the
    /// file from the same stream. Only the first bytes are in memory.
    /// </summary>
    public static async Task<FileUpload> ReadAsync(ReceivedFile file, CancellationToken cancellationToken)
    {
        var head = new byte[FileType.HeadLength];
        file.Content.Position = 0;
        var read = await file.Content.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken);
        file.Content.Position = 0;
        return new FileUpload(file.FileName, file.ClaimedMediaType, file.SizeBytes, head.AsMemory(0, read));
    }
}
