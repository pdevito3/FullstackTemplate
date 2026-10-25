using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace FullstackTemplate.Bff.Management;

/// <summary>
/// A URL on the origin of the SPA, safe to write to a <c>Location</c> header. It is the open-redirect defense of
/// <c>/bff/login</c> and <c>/bff/logout</c>. A URL is local if it is <c>/</c>, or if it starts with <c>/</c> and its
/// second character is not <c>/</c> or <c>\</c>, and it has no control character and no lone surrogate. Absolute URLs
/// and <c>//host</c> fail.
/// </summary>
public sealed class LocalUrl
{
    private LocalUrl(string value) => Value = value;

    public static LocalUrl Root { get; } = new("/");

    /// <summary>
    /// The URL with each non-ASCII character as the percent-encoded UTF-8 bytes (RFC 3987, section 3.1), for
    /// example <c>/caf%C3%A9</c> for <c>/café</c>. A header value can hold only ASCII.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Parses <paramref name="value"/>, for example a decoded <c>returnUrl</c> query. A value with a lone surrogate
    /// fails, because it has no UTF-8 form.
    /// </summary>
    public static bool TryParse(string? value, [NotNullWhen(true)] out LocalUrl? localUrl)
    {
        localUrl = IsLocalPath(value) && ToAscii(value) is { } ascii ? new LocalUrl(ascii) : null;
        return localUrl is not null;
    }

    private static bool IsLocalPath([NotNullWhen(true)] string? url)
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

    private static string? ToAscii(string url)
    {
        if (Ascii.IsValid(url))
            return url;

        var ascii = new StringBuilder(url.Length * 3);
        Span<byte> utf8 = stackalloc byte[4];
        for (var rest = url.AsSpan(); !rest.IsEmpty;)
        {
            if (Rune.DecodeFromUtf16(rest, out var rune, out var consumed) != OperationStatus.Done)
                return null;

            if (rune.IsAscii)
            {
                ascii.Append((char)rune.Value);
            }
            else
            {
                foreach (var b in utf8[..rune.EncodeToUtf8(utf8)])
                    ascii.Append(CultureInfo.InvariantCulture, $"%{b:X2}");
            }

            rest = rest[consumed..];
        }

        return ascii.ToString();
    }
}
