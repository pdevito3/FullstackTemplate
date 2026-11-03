namespace FullstackTemplate.SharedTestHelpers.Fakes.Attachments;

using System.Text;
using FullstackTemplate.Server.Domain.Attachments;

/// <summary>
/// The first bytes of real files of each type, from the specification of each format, not from the registry. A test
/// that checks the type of a file gives one of these.
/// </summary>
public static class SampleFiles
{
    // The PNG signature, then the length and the name of the IHDR chunk
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    // The start of image marker, then the APP0 segment of a JFIF file
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    public static readonly byte[] Gif87 = Ascii("GIF87a\x01\x00\x01\x00");

    public static readonly byte[] Gif89 = Ascii("GIF89a\x01\x00\x01\x00");

    // A RIFF header with the size of the file, then the WEBP form type and a VP8 chunk
    public static readonly byte[] WebP = [.. Ascii("RIFF"), 0x24, 0x00, 0x00, 0x00, .. Ascii("WEBPVP8 ")];

    public static readonly byte[] Pdf = Ascii("%PDF-1.7\n%âãÏÓ\n");

    public static readonly byte[] Text = Ascii("Bring an apron.\n");

    public static readonly byte[] Svg = Ascii("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

    public static readonly byte[] Html = Ascii("<!DOCTYPE html><html><script>alert(1)</script></html>");

    // A ZIP file, as a DOCX or an XLSX file starts
    public static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00];

    private static readonly Dictionary<FileType, byte[]> OfEachType = new()
    {
        [FileType.Png] = Png,
        [FileType.Jpeg] = Jpeg,
        [FileType.Gif] = Gif89,
        [FileType.WebP] = WebP,
        [FileType.Pdf] = Pdf,
    };

    /// <summary>The sample of a type of the registry. <c>FileTypeTests</c> fails when a type has no sample.</summary>
    public static byte[] Of(FileType type)
        => OfEachType.TryGetValue(type, out var head)
            ? head
            : throw new ArgumentException($"SampleFiles has no sample of {type}. Add one from the specification of the format.", nameof(type));

    /// <summary>A file of the length that starts with the first bytes of a sample.</summary>
    public static byte[] OfLength(byte[] head, int length)
    {
        var bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        head.AsSpan(0, Math.Min(head.Length, length)).CopyTo(bytes);
        return bytes;
    }

    /// <summary>What a client sends with a file that starts with the sample.</summary>
    public static FileUpload Upload(string fileName, byte[] head, string? claimedMediaType = null, long? sizeBytes = null)
        => new(fileName, claimedMediaType, sizeBytes ?? head.Length, head.AsMemory(0, Math.Min(head.Length, FileType.HeadLength)));

    private static byte[] Ascii(string text) => Encoding.Latin1.GetBytes(text);
}
