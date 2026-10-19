namespace FullstackTemplate.Server.Resources;

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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

        // The browser calls the BFF on the same origin as the SPA, so the header needs no CORS expose entry
        context.HttpContext.Response.Headers[PaginationMetadata.HeaderName] =
            JsonSerializer.Serialize(PaginationMetadata.From(pagedList), HeaderJsonOptions);
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
