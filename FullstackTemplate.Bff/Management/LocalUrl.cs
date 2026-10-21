namespace FullstackTemplate.Bff.Management;

/// <summary>
/// The open-redirect defense of <c>/bff/login</c> and <c>/bff/logout</c>. A <c>returnUrl</c> is local if it is
/// <c>/</c>, or if it starts with <c>/</c> and its second character is not <c>/</c> or <c>\</c>, and it has no
/// control character. Absolute URLs and <c>//host</c> fail.
/// </summary>
public static class LocalUrl
{
    /// <summary>
    /// True when <paramref name="returnUrl"/> is a local URL. The value goes through <see cref="Uri"/> first,
    /// so a value that is not a URI also fails.
    /// </summary>
    public static bool IsLocal(string returnUrl) =>
        Uri.TryCreate(returnUrl, UriKind.RelativeOrAbsolute, out var uri) && IsLocalPath(uri.ToString());

    private static bool IsLocalPath(string url)
    {
        if (string.IsNullOrEmpty(url) || url[0] != '/')
            return false;

        if (url.Length == 1)
            return true;

        if (url[1] is '/' or '\\')
            return false;

        foreach (var c in url.AsSpan(1))
        {
            if (char.IsControl(c))
                return false;
        }

        return true;
    }
}
