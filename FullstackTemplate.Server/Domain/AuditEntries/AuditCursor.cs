namespace FullstackTemplate.Server.Domain.AuditEntries;

using System.Globalization;
using Exceptions;

/// <summary>
/// The key of the last entry of a page. The next page starts after it in the order of the key, newest first. Its text
/// is "{occurred at, in microseconds since 1970}_{record id}_{ordinal}". A caller sends it back as it got it.
/// </summary>
public readonly record struct AuditCursor(DateTimeOffset OccurredAt, Guid RecordId, int Ordinal)
{
    private const string Parameter = "cursor";

    public static AuditCursor Of(AuditEntry entry) => new(entry.OccurredAt, entry.RecordId, entry.Ordinal);

    /// <exception cref="QueryInputException">The text is not a cursor that <see cref="ToString"/> made.</exception>
    public static AuditCursor Parse(string text)
    {
        var parts = text.Split('_');
        if (parts.Length == 3
            && long.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var microseconds)
            && Guid.TryParseExact(parts[1], "N", out var recordId)
            && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal))
            return new AuditCursor(DateTimeOffset.UnixEpoch.AddTicks(microseconds * TimeSpan.TicksPerMicrosecond),
                recordId, ordinal);

        throw new QueryInputException(Parameter, "The cursor is not valid. Send the nextCursor of the page before.");
    }

    // The database keeps microseconds, so the text keeps each digit of a stored time.
    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture,
            $"{(OccurredAt - DateTimeOffset.UnixEpoch).Ticks / TimeSpan.TicksPerMicrosecond}_{RecordId:N}_{Ordinal}");
}
