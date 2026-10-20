namespace FullstackTemplate.Server.Resources.Entrypoints.Mcp;

/// <summary>
/// One page of a list tool. A <see cref="PagedList{T}"/> serializes as a plain array, so a tool returns
/// this shape to keep the page data that HTTP sends in the X-Pagination header.
/// </summary>
public sealed record McpPage<T>(IReadOnlyList<T> Items, PaginationMetadata Pagination);

public static class McpPage
{
    public static McpPage<T> From<T>(PagedList<T> page) => new(page, PaginationMetadata.From(page));
}
