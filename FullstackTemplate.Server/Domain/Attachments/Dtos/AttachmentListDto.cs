namespace FullstackTemplate.Server.Domain.Attachments.Dtos;

public sealed record AttachmentListDto
{
    /// <summary>The files that an upload to this record can have.</summary>
    public UploadRulesDto UploadRules { get; init; } = default!;
    public List<AttachmentDto> Attachments { get; init; } = [];
}
