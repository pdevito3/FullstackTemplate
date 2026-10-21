using System.ComponentModel.DataAnnotations;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// The settings of the refresh lock, from <c>Bff:Refresh</c>.
/// </summary>
public sealed class RefreshOptions
{
    public const string SectionName = "Bff:Refresh";

    /// <summary>
    /// <c>Bff:Refresh:LockTimeout</c>. The longest wait for the refresh lock of a BFF session. A wait that reaches
    /// it is a temporary refresh failure. The upper limit stays below the default command timeout of 30 seconds,
    /// so that the lock timeout, not the command timeout, ends a long wait.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:00:25")]
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
