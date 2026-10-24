namespace FullstackTemplate.Server.Exceptions;

/// <summary>
/// Thrown when a request breaks a DataAnnotations rule of a request DTO, for example a page number less than 1.
/// The request is not well-formed, so it is a bad input, not a broken business rule.
/// </summary>
public sealed class RequestValidationException(Dictionary<string, string[]> errors)
    : Exception(string.Join(" ", errors.Values.SelectMany(messages => messages)))
{
    /// <summary>
    /// The messages for each request field. The key is the camelCase JSON name of a field of the request or of its
    /// body DTO, for example <c>lastName</c>, or the path of a field of a DTO below them, for example
    /// <c>lines[1].name</c>.
    /// </summary>
    public Dictionary<string, string[]> Errors { get; } = errors;
}
