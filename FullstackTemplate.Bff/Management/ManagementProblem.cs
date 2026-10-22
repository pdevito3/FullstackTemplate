using System.Text.Json;

namespace FullstackTemplate.Bff.Management;

/// <summary>
/// The error response of the management endpoints: a 400 <c>application/problem+json</c> body with a title and
/// one field error.
/// </summary>
public static class ManagementProblem
{
    public const string ContentType = "application/problem+json";

    public static Task WriteAsync(HttpContext context, string title, string field, string error)
    {
        var problem = new HttpValidationProblemDetails(new Dictionary<string, string[]> { [field] = [error] })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
        };

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return context.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, ContentType, context.RequestAborted);
    }
}
