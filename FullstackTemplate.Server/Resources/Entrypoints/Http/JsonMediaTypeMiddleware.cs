namespace FullstackTemplate.Server.Resources.Entrypoints.Http;

using Microsoft.Net.Http.Headers;

/// <summary>
/// Reads a <c>text/json</c> request body as <c>application/json</c>. A minimal API reads only <c>application/json</c>
/// and <c>application/*+json</c>, and gives 415 for <c>text/json</c>. MVC read all three, and the Server still accepts
/// them. The contract states only <c>application/json</c>.
/// </summary>
public sealed class JsonMediaTypeMiddleware(RequestDelegate next)
{
    private const string TextJson = "text/json";

    public Task InvokeAsync(HttpContext context)
    {
        if (MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var mediaType)
            && mediaType.MediaType.Equals(TextJson, StringComparison.OrdinalIgnoreCase))
        {
            mediaType.MediaType = "application/json";
            context.Request.ContentType = mediaType.ToString();
        }

        return next(context);
    }
}
