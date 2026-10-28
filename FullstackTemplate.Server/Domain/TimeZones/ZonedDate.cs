namespace FullstackTemplate.Server.Domain.TimeZones;

/// <summary>
/// A date at a place, such as a clinic day or the due date of a service at a yard. Its instants are the half-open range
/// from <see cref="Start"/> to <see cref="End"/>.
/// </summary>
public sealed record ZonedDate(DateOnly Date, TimeZoneId Zone)
{
    private const int MinutesPerDay = 24 * 60;

    /// <summary>Today at the place. The UTC date is a day off for some hours of each day in most zones.</summary>
    public static ZonedDate TodayAt(TimeZoneId zone, DateTimeOffset now) => new(zone.ToLocal(now).Date, zone);

    /// <summary>Local midnight, or the end of a spring gap at midnight.</summary>
    public DateTimeOffset Start => StartOf(Date);

    /// <summary>The start of the next day. It is not in this day.</summary>
    public DateTimeOffset End => StartOf(Date.AddDays(1));

    public bool Contains(DateTimeOffset instant) => Start <= instant && instant < End;

    private DateTimeOffset StartOf(DateOnly date)
    {
        for (var minute = 0; minute < MinutesPerDay; minute++)
        {
            if (Zone.ToInstant(date, TimeOnly.MinValue.AddMinutes(minute)) is { } instant)
                return instant;
        }

        // A zone that skipped the whole date has no instant on it, so the date is empty and starts with the next one.
        return StartOf(date.AddDays(1));
    }
}
