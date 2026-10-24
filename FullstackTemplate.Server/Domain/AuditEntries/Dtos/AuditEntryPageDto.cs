namespace FullstackTemplate.Server.Domain.AuditEntries.Dtos;

public sealed record AuditEntryPageDto
{
    /// <summary>The entries of the page, newest first.</summary>
    public IReadOnlyList<AuditEntryDto> Items { get; init; } = [];

    /// <summary>Send it as the cursor of the next request to get the next page. Null on the last page.</summary>
    public string? NextCursor { get; init; }
}
