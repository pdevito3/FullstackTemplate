namespace FullstackTemplate.Server.Domain.ObjectDeleteRequests;

/// <summary>The settings of the object deletes, in <c>ObjectStorage:Deletes</c>.</summary>
public sealed record ObjectDeleteOptions
{
    public const string SectionName = "ObjectStorage:Deletes";

    /// <summary>
    /// The time between two runs of <see cref="ObjectDeleteWorker"/>. A download URL of a deleted file works until the
    /// run after the delete.
    /// </summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>The attempts of one request. After the last failed attempt, the request is dead.</summary>
    public int MaxAttempts { get; init; } = 10;

    /// <summary>The delay after the first failed attempt. Each next delay is two times the last one.</summary>
    public TimeSpan FirstRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// The longest time of one call to storage. A storage that does not answer holds the call open, and the claim of
    /// the request keeps other runners off it for two times this value.
    /// </summary>
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromMinutes(1);
}
