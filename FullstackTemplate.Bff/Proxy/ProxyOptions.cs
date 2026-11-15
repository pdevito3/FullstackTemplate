using System.ComponentModel.DataAnnotations;

namespace FullstackTemplate.Bff.Proxy;

public sealed class ProxyOptions
{
    public const string SectionName = "Bff:Proxy";

    /// <summary>
    /// <c>Bff:Proxy:ActivityTimeout</c>. The longest time that a forwarded request waits with no progress, such as for
    /// the answer of a hung Server. A request that reaches it gets a 504.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan ActivityTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// <c>Bff:Proxy:UploadActivityTimeout</c>. The same limit for an upload, a request with a multipart form.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan UploadActivityTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
