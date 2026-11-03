namespace FullstackTemplate.Server.Examples.Workshops;

using System.Collections.Frozen;
using System.Net.Http.Headers;
using Domain;
using DomainEvents;
using Exceptions;
using Models;
using Resources.Storage;

/// <summary>
/// A file of a workshop. The bytes are in object storage under <see cref="StorageKey"/>, and this row holds the
/// metadata. A reader of the workshop gets presigned URLs, so the browser reads the bytes from storage directly.
/// </summary>
public class WorkshopAttachment : BaseEntity, ITenantable
{
    public const string IdPrefix = "wkatt";
    public const int FileNameMaxLength = 255;
    public const int ContentTypeMaxLength = 255;
    public const int SizeMegabytesMaximum = 10;
    public const long SizeBytesMaximum = SizeMegabytesMaximum * 1024L * 1024L;
    public const string DefaultContentType = "application/octet-stream";

    /// <summary>The time that a presigned URL works. The SPA loads the list again before the URLs expire.</summary>
    public static readonly TimeSpan UrlLifetime = TimeSpan.FromMinutes(15);

    public const string FileNameMissingMessage = "The file has no name. Rename the file, then upload it again.";
    public static readonly string FileTooLargeMessage =
        $"A file can be no larger than {SizeMegabytesMaximum} MB. Choose a smaller file.";

    // The browser shows only these types in the page. Each other type, such as SVG or HTML, can hold a script, so it
    // gets only a download URL, which makes the browser save the file and not open it.
    private static readonly FrozenSet<string> PreviewContentTypes = FrozenSet.Create(StringComparer.OrdinalIgnoreCase,
        "image/png", "image/jpeg", "image/gif", "image/webp");

    [NotSensitive] public string TenantId { get; private set; } = default!;
    [NotSensitive] public string WorkshopId { get; private set; } = default!;
    // A file name can name a person, such as "Jane Doe passport.pdf"
    [Sensitive] public string FileName { get; private set; } = default!;
    [NotSensitive] public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    [NotSensitive] public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>The key of the bytes in object storage. The tenant prefix keeps the files of each tenant apart.</summary>
    public string StorageKey => $"tenants/{TenantId}/workshops/{WorkshopId}/attachments/{Id}";

    /// <summary>True when the browser can show the file in the page, so it gets a preview URL.</summary>
    public bool HasPreview => MediaTypeOf(ContentType) is { } mediaType && PreviewContentTypes.Contains(mediaType);

    public static WorkshopAttachment Create(Workshop workshop, WorkshopAttachmentForCreation attachmentForCreation)
    {
        var attachment = new WorkshopAttachment
        {
            TenantId = workshop.TenantId,
            WorkshopId = workshop.Id,
            FileName = attachmentForCreation.FileName,
            ContentType = ContentTypeOf(attachmentForCreation.ContentType),
            SizeBytes = attachmentForCreation.SizeBytes,
            UploadedAt = attachmentForCreation.UploadedAt.ToUniversalTime(),
        };
        if (attachmentForCreation.Id is not null)
            attachment.SetId(attachmentForCreation.Id);

        ValidateAttachment(attachment);
        attachment.QueueDomainEvent(new WorkshopAttachmentAdded(attachment.Id, attachment.WorkshopId));

        return attachment;
    }

    /// <summary>The GET of the bytes as a download, which each attachment has.</summary>
    public PresignedGet DownloadAt(DateTimeOffset now)
        => PresignedGet.Attachment(StorageKey, FileName, ContentType, now + UrlLifetime);

    /// <summary>The GET of the bytes for an <c>img</c> element, or null when the type is not in the preview list.</summary>
    public PresignedGet? PreviewAt(DateTimeOffset now)
        => HasPreview ? PresignedGet.Inline(StorageKey, FileName, ContentType, now + UrlLifetime) : null;

    // The browser names the type from the file extension, so the type is a hint. A type that is not a valid media
    // type of printable ASCII, or that does not fit the column, gives the type of plain bytes, which the browser
    // only downloads. S3 sends the type back in a header, so a control character must not get to it.
    private static string ContentTypeOf(string? contentType)
        => contentType?.Trim() is { Length: > 0 and <= ContentTypeMaxLength } trimmed
           && trimmed.All(c => c is >= ' ' and <= '~')
           && MediaTypeOf(trimmed) is not null
            ? trimmed
            : DefaultContentType;

    private static string? MediaTypeOf(string contentType)
        => MediaTypeHeaderValue.TryParse(contentType, out var parsed) ? parsed.MediaType : null;

    private static void ValidateAttachment(WorkshopAttachment attachment)
    {
        ValidationException.ThrowWhenNullOrWhitespace(attachment.FileName, "fileName", FileNameMissingMessage);
        ValidationException.ThrowWhenLongerThan(attachment.FileName, FileNameMaxLength, "fileName",
            $"A file name can have no more than {FileNameMaxLength} characters. Rename the file, then upload it again.");
        ValidationException.ThrowWhenHasCharacterPostgresCannotStore(attachment.FileName, "fileName",
            "The file name has a character that is not allowed. Rename the file, then upload it again.");
        ValidationException.MustNot(attachment.FileName.IndexOfAny(['/', '\\']) >= 0, "fileName",
            "A file name cannot have a slash or a backslash. Rename the file, then upload it again.");

        ValidationException.Must(attachment.SizeBytes > 0, "sizeBytes", "The file is empty. Choose a file that has content.");
        ValidationException.Must(attachment.SizeBytes <= SizeBytesMaximum, "sizeBytes", FileTooLargeMessage);
    }

    protected WorkshopAttachment() : base(IdPrefix) { } // For EF Core
}
