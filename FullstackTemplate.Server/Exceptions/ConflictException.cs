namespace FullstackTemplate.Server.Exceptions;

/// <summary>
/// Exception thrown when the request conflicts with a record that exists, for example a duplicate unique value. Maps to 409.
/// </summary>
/// <param name="property">The name of the request field, which is the key in the errors of the problem. Empty when no field is known.</param>
/// <param name="message">The message that the problem shows in <c>detail</c>.</param>
/// <param name="innerException">The failure that caused the conflict, if any.</param>
public class ConflictException(string property, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Property { get; } = property;
}
