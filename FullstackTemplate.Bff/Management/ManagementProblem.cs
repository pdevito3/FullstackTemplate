using System.Text.Json;

namespace FullstackTemplate.Bff.Management;

/// <summary>
/// The refusal of the management endpoints: a 400 <c>application/problem+json</c> body with a title and one field
/// error. A browser navigation goes to the <see cref="AuthFailurePage"/> instead.
/// </summary>
public static class ManagementProblem
{
    public const string ContentType = "application/problem+json";

    public static Task RefuseAsync(HttpContext context, AuthFailurePage page, string title, string field, string error)
    {
        if (AuthFailurePage.IsNavigation(context.Request))
        {
            page.Redirect(context);
            return Task.CompletedTask;
        }

        var problem = new HttpValidationProblemDetails(new Dictionary<string, string[]> { [field] = [error] })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
        };

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return context.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, ContentType, context.RequestAborted);
    }
}
