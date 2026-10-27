namespace FullstackTemplate.Server.Resources;

using Databases;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The page data of a <see cref="PagedList{T}"/>, without its item type.
/// </summary>
public interface IPagedList
{
    int PageNumber { get; }
    int TotalPages { get; }
    int PageSize { get; }
    int CurrentPageSize { get; }
    int CurrentStartIndex { get; }
    int CurrentEndIndex { get; }
    int TotalCount { get; }
    bool HasPrevious { get; }
    bool HasNext { get; }
}

public class PagedList<T> : List<T>, IPagedList
{
    public int PageNumber { get; private set; }
    public int TotalPages { get; private set; }
    public int PageSize { get; private set; }
    public int CurrentPageSize { get; private set; }
    public int CurrentStartIndex { get; private set; }
    public int CurrentEndIndex { get; private set; }
    public int TotalCount { get; private set; }

    public bool HasPrevious => PageNumber > 1;
    public bool HasNext => PageNumber < TotalPages;

    public PagedList(List<T> items, int totalCount, int pageNumber, int pageSize)
    {
        TotalCount = totalCount;
        PageSize = pageSize;
        PageNumber = pageNumber;
        CurrentPageSize = items.Count;
        CurrentStartIndex = totalCount == 0 ? 0 : checked(((pageNumber - 1) * pageSize) + 1);
        CurrentEndIndex = totalCount == 0 ? 0 : CurrentStartIndex + CurrentPageSize - 1;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        AddRange(items);
    }
}

public static class PagedList
{
    public static PagedList<T> Create<T>(IQueryable<T> source, int pageNumber, int pageSize)
    {
        var count = source.TagAsCountQuery().Count();
        var items = source.Skip(Offset(pageNumber, pageSize)).Take(pageSize).ToList();
        return new PagedList<T>(items, count, pageNumber, pageSize);
    }

    public static async Task<PagedList<T>> CreateAsync<T>(
        IQueryable<T> source,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var count = await source.TagAsCountQuery().CountAsync(cancellationToken);
        var items = await source
            .Skip(Offset(pageNumber, pageSize))
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedList<T>(items, count, pageNumber, pageSize);
    }

    // BasePaginationParameters limits the page number, so the offset fits in an int.
    // Checked math makes an overflow from another caller throw, not wrap to a negative offset.
    private static int Offset(int pageNumber, int pageSize) => checked((pageNumber - 1) * pageSize);
}
