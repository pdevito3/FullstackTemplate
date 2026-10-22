namespace FullstackTemplate.Server.Exceptions;

/// <summary>
/// The kind of a failure, with no transport in it. Each entrypoint maps a kind to its own error:
/// an HTTP status, a gRPC status code, or an MCP tool error.
/// </summary>
public enum DomainErrorKind
{
    /// <summary>The request breaks a business rule or a FluentValidation rule.</summary>
    Validation,

    /// <summary>The request has a value that the server cannot apply, for example a bad filter.</summary>
    InvalidInput,

    /// <summary>No resource has the ID, or the caller cannot see it.</summary>
    NotFound,

    /// <summary>The caller cannot do this action.</summary>
    Forbidden,

    /// <summary>
    /// The request conflicts with a record that exists, for example a duplicate unique value or an overlap. The
    /// caller must change the value.
    /// </summary>
    Conflict,

    /// <summary>
    /// The caller based the change on a version of the record that is no longer current. The caller must load the
    /// record again and repeat the change.
    /// </summary>
    StaleVersion,

    /// <summary>A server fault. Its message stays on the server.</summary>
    Unexpected,
}

/// <summary>
/// A failure, in the form that all entrypoints show to the caller.
/// </summary>
/// <param name="Kind">The kind of the failure.</param>
/// <param name="Title">A short summary of the kind.</param>
/// <param name="Detail">The message for the caller. Null for a server fault.</param>
/// <param name="Errors">The messages for each named request field. Null when the kind has no field errors.</param>
public sealed record DomainError(
    DomainErrorKind Kind,
    string Title,
    string? Detail,
    Dictionary<string, string[]>? Errors)
{
    private const string ValidationTitle = "One or more validation errors occurred.";
    private const string ConflictTitle = "The request conflicts with a record that exists.";
    private const string StaleVersionTitle = "The record changed after you loaded it.";

    /// <summary>
    /// Classifies an exception. An exception that is not a domain exception is <see cref="DomainErrorKind.Unexpected"/>.
    /// </summary>
    public static DomainError From(Exception exception) => exception switch
    {
        // A 422 has the ValidationProblemDetails shape, so the errors property stays when no field is known
        ValidationException ex => new(
            DomainErrorKind.Validation,
            ValidationTitle,
            ex.Message,
            string.IsNullOrEmpty(ex.Property) ? [] : new() { [ex.Property] = [ex.Message] }),
        FluentValidation.ValidationException ex => new(
            DomainErrorKind.Validation,
            ValidationTitle,
            string.Join(" ", ex.Errors.Select(error => error.ErrorMessage).Distinct()),
            ex.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())),
        RequestValidationException ex => new(DomainErrorKind.InvalidInput, ValidationTitle, ex.Message, ex.Errors),
        QueryInputException ex => new(
            DomainErrorKind.InvalidInput,
            ValidationTitle,
            ex.Message,
            new() { [ex.Parameter] = [ex.Message] }),
        NotFoundException ex => new(DomainErrorKind.NotFound, "The resource was not found.", ex.Message, null),
        ForbiddenAccessException ex => new(DomainErrorKind.Forbidden, "You do not have access to do this.", ex.Message, null),
        StaleVersionException ex => new(
            DomainErrorKind.StaleVersion,
            StaleVersionTitle,
            ex.Message,
            new() { [StaleVersionException.Field] = [ex.Message] }),
        // A conflict keeps the errors property when no field is known, because a 409 has the ValidationProblemDetails shape
        ConflictException ex => new(
            DomainErrorKind.Conflict,
            ConflictTitle,
            ex.Message,
            string.IsNullOrEmpty(ex.Property) ? [] : new() { [ex.Property] = [ex.Message] }),
        _ => new(DomainErrorKind.Unexpected, "An error occurred while processing your request.", null, null),
    };

    /// <summary>
    /// True when the exception is an expected outcome of a bad request, not a server fault.
    /// </summary>
    public static bool IsClientError(Exception exception) => From(exception).Kind != DomainErrorKind.Unexpected;
}
