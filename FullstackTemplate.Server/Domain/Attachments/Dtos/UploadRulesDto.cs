namespace FullstackTemplate.Server.Domain.Attachments.Dtos;

public sealed record UploadRulesDto
{
    public List<FileTypeDto> FileTypes { get; init; } = [];
    /// <summary>The largest file that an upload can have.</summary>
    public int MaxSizeBytes { get; init; }
}
