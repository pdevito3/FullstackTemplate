namespace FullstackTemplate.Server.Examples.Workshops.Mappings;

using Dtos;
using Resources.Storage;

public static class WorkshopAttachmentMapper
{
    /// <summary>Signs the URLs of the attachment. Call it only after the caller passed the read check of the workshop.</summary>
    public static async Task<WorkshopAttachmentDto> ToWorkshopAttachmentDtoAsync(
        this WorkshopAttachment attachment, ObjectStorage storage, DateTimeOffset now)
    {
        var download = attachment.DownloadAt(now);
        var preview = attachment.PreviewAt(now);
        return new WorkshopAttachmentDto
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            // The rules of the entity keep a file at 10 MB or less, so the size fits
            SizeBytes = checked((int)attachment.SizeBytes),
            UploadedAt = attachment.UploadedAt,
            DownloadUrl = await storage.GetPresignedUrlAsync(download),
            PreviewUrl = preview is null ? null : await storage.GetPresignedUrlAsync(preview),
            UrlsExpireAt = download.ExpiresAt,
        };
    }
}
