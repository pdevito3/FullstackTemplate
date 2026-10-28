namespace FullstackTemplate.Server.Resources.Entrypoints;

using Exceptions;
using Http;

/// <summary>
/// Shows a <see cref="DomainError"/> as RFC 7807 problem details. <c>UseExceptionHandler</c> uses
/// <see cref="StatusCodeFor"/> to pick the status, and <c>AddProblemDetails</c> uses
/// <see cref="Customize"/> to fill in the body. A request value that does not bind (a
/// <see cref="BadHttpRequestException"/>) keeps its status, and a 400 gets the errors from <see cref="BindingProblem"/>.
/// Any other exception that is not a domain exception returns a 500.
/// </summary>
public static class DomainProblemDetails
{
    /// <summary>
    /// The <c>type</c> of the problem for a stale version. A 409 for a value that conflicts with a record that exists
    /// keeps the default type, so the type is what tells the two cases apart.
    /// </summary>
    public const string StaleVersionType = "urn:fullstacktemplate:problem:stale-version";

    /// <summary>
    /// The <c>type</c> of the problem for a command with no version that lost the race on each run. The caller sends
    /// it again as it is, and does not load the record first.
    /// </summary>
    public const string ConcurrentChangeType = "urn:fullstacktemplate:problem:concurrent-change";

    // A validation failure is 422: the request is well-formed, but it breaks a business rule.
    public static int StatusCodeFor(Exception exception)
    {
        if (exception is BadHttpRequestException badRequest)
            return badRequest.StatusCode;

        return DomainError.From(exception).Kind switch
        {
            DomainErrorKind.Validation => StatusCodes.Status422UnprocessableEntity,
            DomainErrorKind.InvalidInput => StatusCodes.Status400BadRequest,
            DomainErrorKind.NotFound => StatusCodes.Status404NotFound,
            DomainErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            DomainErrorKind.Conflict or DomainErrorKind.StaleVersion or DomainErrorKind.ConcurrentChange
                => StatusCodes.Status409Conflict,
            DomainErrorKind.Unexpected => StatusCodes.Status500InternalServerError,
        };
    }

    // Detail carries the message because the frontend shows detail before title.
    public static void Customize(ProblemDetailsContext context)
    {
        if (context.Exception is null)
            return;

        if (context.Exception is BadHttpRequestException badRequest)
        {
            if (badRequest.StatusCode != StatusCodes.Status400BadRequest)
                return;
            context.ProblemDetails.Title = BindingProblem.Title;
            context.ProblemDetails.Detail = BindingProblem.DetailFor(badRequest);
            context.ProblemDetails.Extensions["errors"] = BindingProblem.ErrorsFor(badRequest);
            return;
        }

        var error = DomainError.From(context.Exception);
        if (error.Kind == DomainErrorKind.Unexpected)
            return;

        var problem = context.ProblemDetails;
        problem.Title = error.Title;
        problem.Detail = error.Detail;
        problem.Type = error.Kind switch
        {
            DomainErrorKind.StaleVersion => StaleVersionType,
            DomainErrorKind.ConcurrentChange => ConcurrentChangeType,
            _ => problem.Type,
        };
        if (error.Errors is not null)
            problem.Extensions["errors"] = error.Errors;
    }

    /// <summary>
    /// True when the exception is an expected outcome of a bad request, not a server fault.
    /// </summary>
    public static bool IsClientError(Exception exception)
        => exception is BadHttpRequestException || DomainError.IsClientError(exception);
}
