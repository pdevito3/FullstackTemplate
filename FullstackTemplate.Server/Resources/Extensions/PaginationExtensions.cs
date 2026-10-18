namespace FullstackTemplate.Server.Resources.Extensions;

using System.Text.Json;

public static class PaginationExtensions
{
    /// <summary>
    /// Adds pagination metadata to the response headers as X-Pagination.
    /// </summary>
    public static void AddPaginationHeader<T>(this HttpResponse response, PagedList<T> pagedList)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        response.Headers[PaginationMetadata.HeaderName] = JsonSerializer.Serialize(PaginationMetadata.From(pagedList), options);
        response.Headers["Access-Control-Expose-Headers"] = PaginationMetadata.HeaderName;
    }
}
