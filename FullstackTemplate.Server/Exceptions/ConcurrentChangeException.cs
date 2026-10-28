namespace FullstackTemplate.Server.Exceptions;

/// <summary>
/// A command with no version lost the race on its rows on each run of
/// <see cref="Resources.RaceRetryBehavior{TRequest,TResponse}"/>. The caller can send it again as it is.
/// </summary>
public sealed class ConcurrentChangeException(Exception? innerException = null)
    : Exception(ConcurrentMessage, innerException)
{
    public const string ConcurrentMessage =
        "Other users changed this record at the same time, so the change did not save. Send it again.";
}
