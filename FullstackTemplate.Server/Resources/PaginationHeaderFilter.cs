namespace FullstackTemplate.Server.Resources;

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

/// <summary>
/// Adds the X-Pagination response header to each action result that has a <see cref="PagedList{T}"/> value.
/// </summary>
public sealed class PaginationHeaderFilter : IResultFilter
{
    private static readonly JsonSerializerOptions HeaderJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: IPagedList pagedList })
            return;

        var headers = context.HttpContext.Response.Headers;
        headers[PaginationMetadata.HeaderName] = JsonSerializer.Serialize(PaginationMetadata.From(pagedList), HeaderJsonOptions);
        // Append, so that the headers that another component exposed stay
        headers.Append(HeaderNames.AccessControlExposeHeaders, PaginationMetadata.HeaderName);
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
