namespace FullstackTemplate.Server.Exceptions;

/// <summary>
/// Exception thrown when the Server cannot apply a query value, such as <c>filters</c>, <c>sortOrder</c>, <c>cursor</c>, or
/// <c>entityId</c>. Maps to 400.
/// </summary>
/// <param name="parameter">The name of the query parameter, which is the key in the errors of the problem.</param>
/// <param name="message">The message that the problem shows in <c>detail</c>.</param>
/// <param name="innerException">The QueryKit or .NET exception, if there is one. It stays on the Server.</param>
public class QueryInputException(string parameter, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Parameter { get; } = parameter;
}
