namespace FullstackTemplate.Server.Resources.Extensions;

using System.Text.Json;

public static class PaginationExtensions
{
    /// <summary>
    /// Adds pagination metadata to the response headers as X-Pagination.
    /// </summary>
    public static void AddPaginationHeader<T>(this HttpResponse response, PagedList<T> pagedList)
    {
        var paginationMetadata = new
        {
            totalCount = pagedList.TotalCount,
            pageSize = pagedList.PageSize,
            currentPageSize = pagedList.CurrentPageSize,
            currentStartIndex = pagedList.CurrentStartIndex,
            currentEndIndex = pagedList.CurrentEndIndex,
            pageNumber = pagedList.PageNumber,
            totalPages = pagedList.TotalPages,
            hasPrevious = pagedList.HasPrevious,
            hasNext = pagedList.HasNext
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        response.Headers["X-Pagination"] = JsonSerializer.Serialize(paginationMetadata, options);
        response.Headers["Access-Control-Expose-Headers"] = "X-Pagination";
    }
}
