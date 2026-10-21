namespace FullstackTemplate.Bff.HostTrust;

/// <summary>
/// The origin at which the browser reaches the BFF, from <c>Bff:PublicOrigin</c>. The OIDC handler builds
/// <c>redirect_uri</c> and <c>post_logout_redirect_uri</c> from the request, so the BFF sets the scheme and the host
/// of each request to this origin, never to the values that the request or a proxy gives.
/// </summary>
public sealed class PublicOrigin
{
    public const string ConfigurationKey = "Bff:PublicOrigin";

    private PublicOrigin(string scheme, HostString host)
    {
        Scheme = scheme;
        Host = host;
    }

    public string Scheme { get; }

    /// <summary>
    /// The host and, if it is not the default port of the scheme, the port.
    /// </summary>
    public HostString Host { get; }

    /// <summary>
    /// Reads <c>Bff:PublicOrigin</c>. The value must be an absolute http or https URL with no path, query, or
    /// fragment, for example <c>https://bff.example.com</c>.
    /// </summary>
    public static PublicOrigin FromConfiguration(IConfiguration configuration)
    {
        var value = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{ConfigurationKey} configuration is required.");

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || uri.AbsolutePath != "/"
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.UserInfo.Length > 0)
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} must be an http or https origin with no path, query, or fragment, for example https://bff.example.com. The value is '{value}'.");
        }

        return new PublicOrigin(uri.Scheme, HostString.FromUriComponent(uri.Authority));
    }
}
