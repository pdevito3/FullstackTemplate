namespace FullstackTemplate.Server.Resources.Entrypoints.Http;

using System.Text.Json;

/// <summary>
/// Adds the X-Pagination response header to each endpoint result that has a <see cref="PagedList{T}"/> value,
/// for example <c>TypedResults.Ok(pagedList)</c>. <see cref="EndpointDiscovery"/> adds this filter to the versioned group.
/// </summary>
public sealed class PaginationHeaderFilter : IEndpointFilter
{
    private static readonly JsonSerializerOptions HeaderJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);
        AddHeader(context.HttpContext, result);
        return result;
    }

    /// <summary>
    /// Sets the header when the result is a paged list, or an HTTP result with a paged list value. A union result,
    /// for example <c>Results&lt;Ok&lt;PagedList&lt;T&gt;&gt;, NotFound&gt;</c>, wraps the HTTP result, so the filter reads the inner result.
    /// </summary>
    public static void AddHeader(HttpContext httpContext, object? result)
    {
        while (result is INestedHttpResult nested)
            result = nested.Result;

        var pagedList = result switch
        {
            IPagedList list => list,
            IValueHttpResult { Value: IPagedList list } => list,
            _ => null,
        };
        if (pagedList is null)
            return;

        // The browser calls the BFF on the same origin as the SPA, so the header needs no CORS expose entry
        httpContext.Response.Headers[PaginationMetadata.HeaderName] =
            JsonSerializer.Serialize(PaginationMetadata.From(pagedList), HeaderJsonOptions);
    }
}
