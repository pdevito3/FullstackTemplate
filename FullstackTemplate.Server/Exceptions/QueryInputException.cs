namespace FullstackTemplate.Server.Exceptions;

/// <summary>
/// Exception thrown when the Server cannot apply a <c>filters</c> or <c>sortOrder</c> value of a list request. Maps to 400.
/// </summary>
/// <param name="parameter">The name of the query parameter, which is the key in the errors of the problem.</param>
/// <param name="message">The message that the problem shows in <c>detail</c>.</param>
/// <param name="innerException">The QueryKit or .NET exception. It stays on the Server.</param>
public class QueryInputException(string parameter, string message, Exception innerException)
    : Exception(message, innerException)
{
    public string Parameter { get; } = parameter;
}
