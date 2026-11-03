namespace FullstackTemplate.Server.Domain.Attachments;

/// <summary>
/// A type of file that an upload can have. The first bytes of a file name its type, so a renamed file does not pass
/// as another type. Add a type only when each of its files starts with fixed bytes. A text type, such as CSV or SVG,
/// has no such bytes.
/// </summary>
public sealed class FileType
{
    public static readonly FileType Png = new("PNG", "a PNG image", "image/png", [".png"], canPreview: true,
        ["89 50 4E 47 0D 0A 1A 0A"], ["image/x-png"]);

    public static readonly FileType Jpeg = new("JPEG", "a JPEG image", "image/jpeg", [".jpg", ".jpeg"], canPreview: true,
        ["FF D8 FF"], ["image/jpg", "image/pjpeg"]);

    public static readonly FileType Gif = new("GIF", "a GIF image", "image/gif", [".gif"], canPreview: true,
        ["47 49 46 38 37 61", "47 49 46 38 39 61"]);

    public static readonly FileType WebP = new("WebP", "a WebP image", "image/webp", [".webp"], canPreview: true,
        ["52 49 46 46 ?? ?? ?? ?? 57 45 42 50"]);

    public static readonly FileType Pdf = new("PDF", "a PDF document", "application/pdf", [".pdf"], canPreview: false,
        ["25 50 44 46 2D"], ["application/x-pdf"]);

    /// <summary>Each type that a rule set can allow.</summary>
    public static IReadOnlyList<FileType> All { get; } = [Png, Jpeg, Gif, WebP, Pdf];

    /// <summary>The number of first bytes that each signature fits in.</summary>
    public static int HeadLength { get; } = All.Max(t => t._signatures.Max(s => s.Length));

    private readonly byte?[][] _signatures;
    private readonly string[] _otherMediaTypes;

    private FileType(string name, string description, string mediaType, string[] extensions, bool canPreview,
        string[] signatures, string[]? otherMediaTypes = null)
    {
        Name = name;
        Description = description;
        MediaType = mediaType;
        Extensions = extensions;
        CanPreview = canPreview;
        _signatures = signatures.Select(BytesOf).ToArray();
        _otherMediaTypes = otherMediaTypes ?? [];
    }

    /// <summary>The name that the screen shows, for example "PNG".</summary>
    public string Name { get; }

    /// <summary>The words of a message, with the article, for example "a PNG image".</summary>
    public string Description { get; }

    /// <summary>The media type that storage keeps and that a download sends.</summary>
    public string MediaType { get; }

    /// <summary>The extensions of a file name of this type, lower case, with the dot.</summary>
    public IReadOnlyList<string> Extensions { get; }

    /// <summary>
    /// True when the browser can show the file in the page. Only a raster image is true. An SVG or an HTML file can
    /// hold a script, so it never shows in the page: it gets only a download URL, and the browser saves it.
    /// </summary>
    public bool CanPreview { get; }

    public string ExtensionMismatchMessage
        => $"The file is {Description}, so its name must end in {string.Join(" or ", Extensions)}. Rename the file, then upload it again.";

    public string MediaTypeMismatchMessage
        => $"The type that the upload gives does not match the file, which is {Description}. Upload the file again.";

    public static FileType? OfMediaType(string mediaType)
        => All.FirstOrDefault(t => t.MediaType.Equals(mediaType, StringComparison.OrdinalIgnoreCase));

    /// <summary>True when the first bytes of a file are a signature of this type.</summary>
    public bool StartsFile(ReadOnlySpan<byte> head)
    {
        foreach (var signature in _signatures)
        {
            if (Matches(signature, head))
                return true;
        }

        return false;
    }

    public bool NamesFile(string fileName)
        => Extensions.Any(e => fileName.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    /// <summary>True for the media type of this type, and for an older name of it that some systems still send.</summary>
    public bool HasMediaType(string mediaType)
        => MediaType.Equals(mediaType, StringComparison.OrdinalIgnoreCase)
           || _otherMediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase);

    private static bool Matches(byte?[] signature, ReadOnlySpan<byte> head)
    {
        if (head.Length < signature.Length)
            return false;

        for (var i = 0; i < signature.Length; i++)
        {
            if (signature[i] is { } expected && head[i] != expected)
                return false;
        }

        return true;
    }

    // "??" is a byte that can have any value, such as the size of a RIFF file
    private static byte?[] BytesOf(string hex)
        => hex.Split(' ').Select(b => b == "??" ? (byte?)null : Convert.ToByte(b, 16)).ToArray();

    public override string ToString() => Name;
}
