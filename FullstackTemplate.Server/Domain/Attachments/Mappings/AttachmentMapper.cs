namespace FullstackTemplate.Server.Domain.Attachments.Mappings;

using Dtos;
using Resources.Storage;

public static class AttachmentMapper
{
    /// <summary>Signs the URLs of each file. Call it only after the caller passed the read check of the owner.</summary>
    public static async Task<AttachmentListDto> ToAttachmentListDtoAsync<TAttachment>(
        this IReadOnlyList<TAttachment> attachments, UploadRules rules, ObjectStorage storage, DateTimeOffset now)
        where TAttachment : IAttachment
    {
        var dtos = new List<AttachmentDto>(attachments.Count);
        foreach (var attachment in attachments)
            dtos.Add(await attachment.ToAttachmentDtoAsync(storage, now));
        return new AttachmentListDto { UploadRules = rules.ToUploadRulesDto(), Attachments = dtos };
    }

    /// <summary>Signs the URLs of the file. Call it only after the caller passed the read check of the owner.</summary>
    public static async Task<AttachmentDto> ToAttachmentDtoAsync(this IAttachment attachment, ObjectStorage storage,
        DateTimeOffset now)
    {
        var file = attachment.File;
        var download = file.DownloadAt(now);
        var preview = file.PreviewAt(now);
        return new AttachmentDto
        {
            Id = attachment.Id,
            FileName = file.Name,
            MediaType = file.MediaType,
            // The upload rules keep each size under 2 GB, so the size fits
            SizeBytes = checked((int)file.SizeBytes),
            UploadedAt = attachment.UploadedAt,
            DownloadUrl = await storage.GetPresignedUrlAsync(download),
            PreviewUrl = preview is null ? null : await storage.GetPresignedUrlAsync(preview),
            UrlsExpireAt = download.ExpiresAt,
        };
    }

    public static UploadRulesDto ToUploadRulesDto(this UploadRules rules) => new()
    {
        FileTypes = rules.FileTypes
            .Select(t => new FileTypeDto { Name = t.Name, MediaType = t.MediaType, Extensions = [.. t.Extensions] })
            .ToList(),
        MaxSizeBytes = checked((int)rules.MaxSizeBytes),
    };
}
