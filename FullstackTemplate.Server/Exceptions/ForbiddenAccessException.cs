namespace FullstackTemplate.Server.Exceptions;

/// <summary>
/// Exception thrown when the caller is known but cannot do this action. Maps to 403.
/// </summary>
public class ForbiddenAccessException(string message) : Exception(message);
