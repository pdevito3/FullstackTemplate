namespace FullstackTemplate.Server.Exceptions;

using Microsoft.EntityFrameworkCore;
using Npgsql;

/// <summary>
/// Maps domain exceptions to RFC 7807 problem details. <c>UseExceptionHandler</c> uses
/// <see cref="StatusCodeFor"/> to pick the status, and <c>AddProblemDetails</c> uses
/// <see cref="Customize"/> to fill in the body. An exception that is not mapped here returns a 500.
/// </summary>
public static class DomainProblemDetails
{
    // A validation failure is 422: the request is well-formed, but it breaks a business rule.
    // A duplicate unique value is 409: the request conflicts with a record that exists.
    public static int StatusCodeFor(Exception exception) => exception switch
    {
        ValidationException or FluentValidation.ValidationException => StatusCodes.Status422UnprocessableEntity,
        QueryInputException => StatusCodes.Status400BadRequest,
        NotFoundException => StatusCodes.Status404NotFound,
        ForbiddenAccessException => StatusCodes.Status403Forbidden,
        ConflictException => StatusCodes.Status409Conflict,
        _ when IsUniqueViolation(exception) => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    // A handler checks for a duplicate first, but two requests can pass the check at the same time.
    // Then the unique index stops the second save. That is a client conflict, not a server fault.
    private static bool IsUniqueViolation(Exception exception) =>
        exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };

    /// <summary>
    /// The request field of each unique index. The problem names the field, not the index, because the index name shows the schema.
    /// A unit test makes sure that each unique index in the model has an entry.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> UniqueIndexFields = new Dictionary<string, string>
    {
        ["ix_tenants_name"] = "name",
        ["ix_users_identifier"] = "identifier",
    };

    /// <summary>
    /// True when the exception is an expected outcome of a bad request, not a server fault.
    /// </summary>
    public static bool IsClientError(Exception exception) =>
        StatusCodeFor(exception) < StatusCodes.Status500InternalServerError;

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
            case ConflictException ex:
                problem.Title = "The request conflicts with a record that exists.";
                problem.Detail = ex.Message;
                problem.Extensions["errors"] = new Dictionary<string, string[]> { [ex.Property] = [ex.Message] };
                break;
            // The constraint name and the values stay out of the response, because they show the schema and other rows.
            case { } ex when IsUniqueViolation(ex):
                problem.Title = "The request conflicts with a record that exists.";
                problem.Detail = "A record with this value already exists.";
                problem.Extensions["errors"] = UniqueViolationField(ex) is { } field
                    ? new Dictionary<string, string[]> { [field] = [problem.Detail] }
                    : new Dictionary<string, string[]>();
                break;
            case QueryInputException ex:
                problem.Title = "One or more validation errors occurred.";
                problem.Detail = ex.Message;
                problem.Extensions["errors"] = new Dictionary<string, string[]> { [ex.Parameter] = [ex.Message] };
                break;
        }
    }
    private static string? UniqueViolationField(Exception exception) =>
        exception.InnerException is PostgresException { ConstraintName: { } index } && UniqueIndexFields.TryGetValue(index, out var field)
            ? field
            : null;
}
