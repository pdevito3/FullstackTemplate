namespace FullstackTemplate.Server.Exceptions;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using QueryKit.Exceptions;

/// <summary>
/// Maps domain exceptions to RFC 7807 problem details. <c>UseExceptionHandler</c> uses
/// <see cref="StatusCodeFor"/> to pick the status, and <c>AddProblemDetails</c> uses
/// <see cref="Customize"/> to fill in the body. An exception that is not mapped here returns a 500.
/// </summary>
public static class DomainProblemDetails
{
    // A validation failure is 422: the request is well-formed, but it breaks a business rule.
    public static int StatusCodeFor(Exception exception) => exception switch
    {
        ValidationException or FluentValidation.ValidationException => StatusCodes.Status422UnprocessableEntity,
        _ when IsQueryInputError(exception) => StatusCodes.Status400BadRequest,
        NotFoundException => StatusCodes.Status404NotFound,
        ForbiddenAccessException => StatusCodes.Status403Forbidden,
        _ when IsUniqueViolation(exception) => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError,
    };

    // A handler checks for a duplicate first, but two requests can pass the check at the same time.
    // Then the unique index stops the second save. That is a client conflict, not a server fault.
    private static bool IsUniqueViolation(Exception exception) =>
        exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };

    /// <summary>
    /// True when the exception is an expected outcome of a bad request, not a server fault.
    /// </summary>
    public static bool IsClientError(Exception exception) =>
        StatusCodeFor(exception) < StatusCodes.Status500InternalServerError;

    // A filters or sortOrder value that QueryKit cannot read is a fault in the request, not in the server.
    // QueryKit throws a QueryKitException for each bad filter or sort. These two come from the server setup, so they stay 500.
    private static bool IsQueryInputError(Exception exception) => exception is QueryKitException
        and not QueryKitDbContextTypeException
        and not SoundsLikeNotImplementedException;

    // Detail carries the message because the frontend shows detail before title.
    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        switch (context.Exception)
        {
            case ValidationException ex:
                problem.Title = "One or more validation errors occurred.";
                problem.Detail = ex.Message;
                problem.Extensions["errors"] = string.IsNullOrEmpty(ex.Property)
                    ? new Dictionary<string, string[]>()
                    : new Dictionary<string, string[]> { [ex.Property] = [ex.Message] };
                break;
            case FluentValidation.ValidationException ex:
                problem.Title = "One or more validation errors occurred.";
                problem.Detail = string.Join(" ", ex.Errors.Select(error => error.ErrorMessage).Distinct());
                problem.Extensions["errors"] = ex.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
                break;
            case NotFoundException ex:
                problem.Title = "The resource was not found.";
                problem.Detail = ex.Message;
                break;
            case ForbiddenAccessException ex:
                problem.Title = "You do not have access to do this.";
                problem.Detail = ex.Message;
                break;
            // The constraint name and the values stay out of the response, because they show the schema and other rows.
            case { } ex when IsUniqueViolation(ex):
                problem.Title = "One or more validation errors occurred.";
                problem.Detail = "A record with this value already exists.";
                problem.Extensions["errors"] = new Dictionary<string, string[]>();
                break;
            case { } ex when IsQueryInputError(ex):
                var parameter = ex is SortParsingException ? "sortOrder" : "filters";
                problem.Title = "One or more validation errors occurred.";
                problem.Detail = ex.Message;
                problem.Extensions["errors"] = new Dictionary<string, string[]> { [parameter] = [ex.Message] };
                break;
        }
    }
}
