using System.ComponentModel.DataAnnotations;

namespace FullstackTemplate.Bff.Sessions;

/// <summary>
/// The lifetime settings of a BFF session, from <c>Bff:Session</c>.
/// </summary>
public sealed class BffSessionOptions
{
    public const string SectionName = "Bff:Session";

    /// <summary>
    /// <c>Bff:Session:SlidingExpiration</c>. The cookie handler renews it on a request after half of it is gone.
    /// </summary>
    [Range(typeof(TimeSpan), "00:01:00", "3650.00:00:00")]
    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromDays(14);

    /// <summary>
    /// <c>Bff:Session:AbsoluteLifetime</c>. A renewal never moves the expiry past the creation time plus this value.
    /// </summary>
    [Range(typeof(TimeSpan), "00:01:00", "3650.00:00:00")]
    public TimeSpan AbsoluteLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// <c>Bff:Session:CleanupInterval</c>. The time between two runs of the cleanup of the ended rows.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(10);
}
