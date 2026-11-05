namespace FullstackTemplate.Server.Exceptions;

/// <summary>
/// Exception thrown when the record exists but can no longer be used, for example an invite that expired. Maps to 410.
/// </summary>
public class GoneException(string message) : Exception(message);
