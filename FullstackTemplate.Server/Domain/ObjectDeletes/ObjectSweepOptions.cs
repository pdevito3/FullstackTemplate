namespace FullstackTemplate.Server.Domain.ObjectDeletes;

using Attachments;
using BackWave.Core;

/// <summary>The settings of the sweep of the bucket, in <c>ObjectStorage:Sweep</c>.</summary>
public sealed record ObjectSweepOptions
{
    public const string SectionName = "ObjectStorage:Sweep";

    /// <summary>The queue of the sweep, so that a long sweep does not hold up the object deletes.</summary>
    public const string Queue = "object-sweep";

    /// <summary>The largest skew of the clock of this Server from the clock of S3, as S3 refuses a request with more.</summary>
    private static readonly TimeSpan LargestClockSkew = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The cron of the sweep in UTC, with 5 fields, or 6 fields with the seconds first. Each run lists the whole bucket.
    /// </summary>
    public string Schedule { get; init; } = "0 3 * * *";

    /// <summary>
    /// The time past <see cref="AttachmentOptions.AddTimeout"/> that the sweep keeps an object that no row names. It
    /// covers the skew of the clocks and a commit that lands after its add stopped.
    /// </summary>
    public TimeSpan Margin { get; init; } = TimeSpan.FromHours(1);

    public CronExpression Cron => CronExpression.Parse(Schedule);

    /// <summary>The age up to which the sweep keeps an object that no row names, as the add of its row can still save it.</summary>
    public TimeSpan GraceFor(AttachmentOptions attachments) => attachments.AddTimeout + Margin;

    /// <exception cref="InvalidOperationException">A setting is out of its range.</exception>
    public static ObjectSweepOptions From(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<ObjectSweepOptions>() ?? new ObjectSweepOptions();
        if (options.Margin <= LargestClockSkew)
            throw new InvalidOperationException($"{SectionName}:Margin must be over {LargestClockSkew}, the largest skew of the clock of S3.");
        try
        {
            _ = options.Cron;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new InvalidOperationException($"{SectionName}:Schedule is not a cron expression.", exception);
        }

        return options;
    }
}
