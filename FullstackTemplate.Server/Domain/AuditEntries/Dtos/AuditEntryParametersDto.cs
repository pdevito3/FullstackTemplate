namespace FullstackTemplate.Server.Domain.AuditEntries.Dtos;

using System.ComponentModel.DataAnnotations;
using Resources;

public sealed record AuditEntryParametersDto
{
    /// <summary>The id of an aggregate root, for example a user. Null for each entry of the tenant.</summary>
    public string? EntityId { get; init; }

    /// <summary>The <see cref="AuditEntryPageDto.NextCursor"/> of the page before. Null for the first page.</summary>
    public string? Cursor { get; init; }

    [Range(1, int.MaxValue)]
    public int PageSize { get; init; } = BasePaginationParameters.DefaultPageSizeValue;

    /// <summary>
    /// Makes the parameters from the values of a caller. A null value keeps the default, and an empty value means none.
    /// A page size larger than <see cref="BasePaginationParameters.MaxPageSizeLimit"/> becomes that limit.
    /// </summary>
    public static AuditEntryParametersDto From(string? entityId, string? cursor, int? pageSize) => new()
    {
        EntityId = string.IsNullOrEmpty(entityId) ? null : entityId,
        Cursor = string.IsNullOrEmpty(cursor) ? null : cursor,
        PageSize = Math.Min(pageSize ?? BasePaginationParameters.DefaultPageSizeValue,
            BasePaginationParameters.MaxPageSizeLimit),
    };
}
