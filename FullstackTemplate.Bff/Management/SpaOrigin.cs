namespace FullstackTemplate.Bff.Management;

/// <summary>
/// The origin at which the browser opens the SPA.
/// </summary>
public sealed class SpaOrigin
{
    private SpaOrigin(string? devServer) => DevServer = devServer;

    /// <summary>
    /// The origin of the Vite dev server, for example <c>http://localhost:5173</c>, or null when the BFF serves the
    /// SPA from <c>wwwroot</c>.
    /// </summary>
    public string? DevServer { get; }

    /// <summary>
    /// Reads <c>services:webfrontend:https:0</c>, then <c>services:webfrontend:http:0</c>. Only the scheme, the host,
    /// and the port of the value are kept.
    /// </summary>
    public static SpaOrigin FromConfiguration(IConfiguration configuration)
    {
        var value = configuration["services:webfrontend:https:0"] is { Length: > 0 } https
            ? https
            : configuration["services:webfrontend:http:0"];
        if (string.IsNullOrWhiteSpace(value))
            return new SpaOrigin(null);

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || uri.UserInfo.Length > 0)
        {
            throw new InvalidOperationException($"services:webfrontend must be an http or https URL with no user info. The value is '{value}'.");
        }

        return new SpaOrigin(uri.GetLeftPart(UriPartial.Authority));
    }

    /// <summary>
    /// The URL to send the browser to for <paramref name="localUrl"/>.
    /// </summary>
    public string Resolve(LocalUrl localUrl) => DevServer is null ? localUrl.Value : DevServer + localUrl.Value;
}
