namespace FullstackTemplate.Server.Resources.Extensions;

using System.Text.Json;

public static class PaginationExtensions
{
    private static readonly JsonSerializerOptions HeaderJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Adds pagination metadata to the response headers as X-Pagination.
    /// </summary>
    public static void AddPaginationHeader<T>(this HttpResponse response, PagedList<T> pagedList)
    {
        response.Headers[PaginationMetadata.HeaderName] = JsonSerializer.Serialize(PaginationMetadata.From(pagedList), HeaderJsonOptions);
        response.Headers["Access-Control-Expose-Headers"] = PaginationMetadata.HeaderName;
    }
}
