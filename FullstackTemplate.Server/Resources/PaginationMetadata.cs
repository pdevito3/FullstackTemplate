namespace FullstackTemplate.Server.Resources;

/// <summary>
/// The pagination data that a list endpoint sends in the X-Pagination response header.
/// </summary>
public sealed record PaginationMetadata
{
    public const string HeaderName = "X-Pagination";

    public int TotalCount { get; init; }
    public int PageSize { get; init; }
    public int CurrentPageSize { get; init; }
    public int CurrentStartIndex { get; init; }
    public int CurrentEndIndex { get; init; }
    public int PageNumber { get; init; }
    public int TotalPages { get; init; }
    public bool HasPrevious { get; init; }
    public bool HasNext { get; init; }

    public static PaginationMetadata From<T>(PagedList<T> pagedList) => new()
    {
        TotalCount = pagedList.TotalCount,
        PageSize = pagedList.PageSize,
        CurrentPageSize = pagedList.CurrentPageSize,
        CurrentStartIndex = pagedList.CurrentStartIndex,
        CurrentEndIndex = pagedList.CurrentEndIndex,
        PageNumber = pagedList.PageNumber,
        TotalPages = pagedList.TotalPages,
        HasPrevious = pagedList.HasPrevious,
        HasNext = pagedList.HasNext,
    };
}
