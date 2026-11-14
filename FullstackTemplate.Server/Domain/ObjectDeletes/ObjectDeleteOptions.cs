namespace FullstackTemplate.Server.Domain.ObjectDeletes;

using BackWave.Core;
using BackWave.Hosting;

/// <summary>The settings of the object deletes, in <c>ObjectStorage:Deletes</c>.</summary>
public sealed record ObjectDeleteOptions
{
    public const string SectionName = "ObjectStorage:Deletes";

    /// <summary>The queue of the object delete jobs. Its worker group retries with these settings.</summary>
    public const string Queue = "object-deletes";

    /// <summary>The attempts of one job. After the last failed attempt, BackWave dead-letters the job.</summary>
    public int MaxAttempts { get; init; } = 10;

    /// <summary>The delay after the first failed attempt. Each next delay is two times the last one.</summary>
    public TimeSpan FirstRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromHours(1);

    /// <summary>The longest time of one call to storage. A storage that does not answer holds the call open.</summary>
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The delay after the failed attempt <paramref name="failedAttempt"/>, 1 for the first attempt. It doubles with
    /// each attempt up to <see cref="MaxRetryDelay"/>.
    /// </summary>
    public TimeSpan RetryDelayAfter(int failedAttempt)
    {
        var delay = FirstRetryDelay;
        // Each step stops at the largest delay, so a large attempt does not overflow the TimeSpan
        for (var attempt = 1; attempt < failedAttempt && delay < MaxRetryDelay; attempt++)
            delay = delay < MaxRetryDelay / 2 ? delay * 2 : MaxRetryDelay;
        return delay < MaxRetryDelay ? delay : MaxRetryDelay;
    }

    /// <summary>The worker group of <see cref="Queue"/>. A storage outage fills only this group.</summary>
    public WorkerGroupOptions WorkerGroup() => new()
    {
        Name = Queue,
        Policy = new DispatchPolicy.Strict(Queue),
        RetryPolicy = new RetryPolicy { MaxAttempts = MaxAttempts, Backoff = RetryDelayAfter },
    };

    /// <summary>Reads the settings at startup, because a worker group takes its retry when the host is built.</summary>
    /// <exception cref="InvalidOperationException">A setting is out of its range.</exception>
    public static ObjectDeleteOptions From(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<ObjectDeleteOptions>() ?? new ObjectDeleteOptions();
        // A delay of zero would run a failed job again at once, with no end
        if (options.FirstRetryDelay <= TimeSpan.Zero || options.MaxRetryDelay < options.FirstRetryDelay
            || options.AttemptTimeout <= TimeSpan.Zero || options.MaxAttempts is < 1 or > RetryDisposition.MaxAttemptCeiling)
            throw new InvalidOperationException(
                $"{SectionName} needs times over zero, a MaxRetryDelay not under FirstRetryDelay, and MaxAttempts from 1 to {RetryDisposition.MaxAttemptCeiling}.");
        return options;
    }
}
