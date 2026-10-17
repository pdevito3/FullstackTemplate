namespace FullstackTemplate.Server.Exceptions;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Maps domain exceptions to RFC 7807 problem responses. An exception that is not mapped here falls
/// through to <c>UseExceptionHandler</c>, which returns a 500.
/// </summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = ToProblemDetails(exception);
        if (problem is null)
            return false;

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    /// <summary>
    /// True when the exception is an expected outcome of a bad request, not a server fault.
    /// </summary>
    public static bool IsClientError(Exception exception) => ToProblemDetails(exception) is not null;

    // A validation failure is 422: the request is well-formed, but it breaks a business rule. Detail
    // carries the message because the frontend shows detail before title.
    private static ProblemDetails? ToProblemDetails(Exception exception) => exception switch
    {
        ValidationException ex => new ValidationProblemDetails(string.IsNullOrEmpty(ex.Property)
            ? new Dictionary<string, string[]>()
            : new Dictionary<string, string[]> { [ex.Property] = [ex.Message] })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Detail = ex.Message,
        },
        FluentValidation.ValidationException ex => new ValidationProblemDetails(ex.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()))
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Detail = string.Join(" ", ex.Errors.Select(error => error.ErrorMessage).Distinct()),
        },
        NotFoundException ex => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "The resource was not found.",
            Detail = ex.Message,
        },
        _ => null,
    };
}
