namespace FullstackTemplate.Server.Resources;

using System.Buffers;
using System.Text;

/// <summary>
/// Cleans a value that comes from the caller, such as a request path or a header, before it goes into a log. The
/// decoded request path keeps <c>%0D%0A</c> as CR and LF, so without this a text log sink shows a log line that the
/// caller wrote.
/// </summary>
public static class LogValue
{
    // The C0 controls (CR and LF too), DEL, the C1 controls, and the Unicode line and paragraph separators.
    private static readonly SearchValues<char> Unsafe = SearchValues.Create(
        Enumerable.Range(0x00, 0x20)
            .Concat(Enumerable.Range(0x7F, 0x21))
            .Append(0x2028)
            .Append(0x2029)
            .Select(c => (char)c)
            .ToArray());

    /// <summary>
    /// <paramref name="value"/> with each control character and each Unicode line or paragraph separator
    /// removed. Returns the same string when it has none of them.
    /// </summary>
    public static string? Clean(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var first = value.AsSpan().IndexOfAny(Unsafe);
        if (first < 0)
            return value;

        var builder = new StringBuilder(value.Length);
        builder.Append(value, 0, first);
        foreach (var c in value.AsSpan(first))
        {
            if (!Unsafe.Contains(c))
                builder.Append(c);
        }

        return builder.ToString();
    }
}
