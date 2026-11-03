namespace FullstackTemplate.Server.Domain.Attachments;

using Exceptions;
using Resources.Storage;

/// <summary>
/// A file in object storage: its name, its media type, its size, and the key of its bytes. Only
/// <see cref="Accept"/> makes one, so each stored file passed the upload rules of its use.
/// </summary>
public sealed partial class StoredFile : ValueObject
{
    public const int NameMaxLength = 255;
    public const int MediaTypeMaxLength = 100;
    public const int StorageKeyMaxLength = 1024;

    /// <summary>The time that a presigned URL works. The SPA loads the list again before the URLs expire.</summary>
    public static readonly TimeSpan UrlLifetime = TimeSpan.FromMinutes(15);

    public string Name { get; private set; } = default!;
    public string MediaType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = default!;

    /// <summary>True when the browser can show the file in the page. A type that the registry no longer has is false.</summary>
    public bool CanPreview => FileType.OfMediaType(MediaType)?.CanPreview == true;

    /// <summary>Checks the upload with the rules of its use, and gives the file that storage keeps under the key.</summary>
    /// <param name="upload">What the client sent with the file.</param>
    /// <param name="rules">The upload rules of the use, such as the rules of one entity.</param>
    /// <param name="storageKey">The key of the bytes. Start it with <c>tenants/{tenantId}/</c>, so that the files of
    /// each tenant stay apart.</param>
    public static StoredFile Accept(FileUpload upload, UploadRules rules, string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Length > StorageKeyMaxLength)
            throw new ArgumentException($"A storage key has 1 to {StorageKeyMaxLength} characters.", nameof(storageKey));

        ValidateName(upload.FileName);
        var type = rules.TypeOf(upload);
        return new StoredFile
        {
            Name = upload.FileName,
            MediaType = type.MediaType,
            SizeBytes = upload.SizeBytes,
            StorageKey = storageKey,
        };
    }

    /// <summary>A GET that makes the browser save the file under its name.</summary>
    public PresignedGet DownloadAt(DateTimeOffset now)
        => PresignedGet.Attachment(StorageKey, Name, MediaType, now + UrlLifetime);

    /// <summary>A GET that an img element can show, or null when the type cannot show in the page.</summary>
    public PresignedGet? PreviewAt(DateTimeOffset now)
        => CanPreview ? PresignedGet.Inline(StorageKey, Name, MediaType, now + UrlLifetime) : null;

    private static void ValidateName(string name)
    {
        ValidationException.ThrowWhenNullOrWhitespace(name, "fileName", "The file has no name. Rename the file, then upload it again.");
        ValidationException.ThrowWhenLongerThan(name, NameMaxLength, "fileName",
            $"A file name can have no more than {NameMaxLength} characters. Rename the file, then upload it again.");
        ValidationException.ThrowWhenHasCharacterPostgresCannotStore(name, "fileName",
            "The file name has a character that is not allowed. Rename the file, then upload it again.");
        ValidationException.MustNot(name.IndexOfAny(['/', '\\']) >= 0, "fileName",
            "A file name cannot have a slash or a backslash. Rename the file, then upload it again.");
    }

    private StoredFile() { } // EF Core
}
