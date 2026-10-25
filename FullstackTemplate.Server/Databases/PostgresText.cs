namespace FullstackTemplate.Server.Databases;

using System.Buffers;
using System.Text;

public static class PostgresText
{
    /// <summary>
    /// False when <paramref name="value"/> has a NUL, which Postgres text cannot hold, or a lone surrogate, which UTF-8
    /// cannot encode. A query that sends such a value as a parameter fails.
    /// </summary>
    public static bool CanStore(string value)
    {
        var rest = value.AsSpan();
        while (!rest.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(rest, out var rune, out var consumed) != OperationStatus.Done || rune.Value == 0)
                return false;
            rest = rest[consumed..];
        }

        return true;
    }
}
