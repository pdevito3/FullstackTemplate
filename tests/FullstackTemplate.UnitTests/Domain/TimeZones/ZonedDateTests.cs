namespace FullstackTemplate.UnitTests.Domain.TimeZones;

using System.Globalization;
using FullstackTemplate.Server.Domain.TimeZones;
using Shouldly;

/// <summary>
/// Chicago is on CDT (UTC-5) in October 2026. Tongatapu is on UTC+13 all year. Santiago moves from UTC-4 to UTC-3 at
/// local midnight on September 6, 2026, so that day has no midnight.
/// </summary>
public class ZonedDateTests
{
    private static readonly TimeZoneId Chicago = TimeZoneId.Of("America/Chicago");
    private static readonly TimeZoneId Santiago = TimeZoneId.Of("America/Santiago");

    [Test]
    [Arguments("2026-10-27T23:30:00Z", "America/Chicago", "2026-10-27")]
    [Arguments("2026-10-28T00:30:00Z", "America/Chicago", "2026-10-27")]
    [Arguments("2026-10-27T23:30:00Z", "Pacific/Tongatapu", "2026-10-28")]
    [Arguments("2026-10-28T00:30:00Z", "Pacific/Tongatapu", "2026-10-28")]
    public void today_is_the_date_at_the_place_not_the_utc_date(string now, string zone, string expected)
    {
        // Act
        var today = ZonedDate.TodayAt(TimeZoneId.Of(zone), DateTimeOffset.Parse(now, CultureInfo.InvariantCulture));

        // Assert
        today.Date.ShouldBe(DateOnly.Parse(expected, CultureInfo.InvariantCulture));
    }

    [Test]
    [Arguments("2026-10-27T23:30:00Z", "America/Chicago")]
    [Arguments("2026-10-28T00:30:00Z", "America/Chicago")]
    [Arguments("2026-10-27T23:30:00Z", "Pacific/Tongatapu")]
    [Arguments("2026-10-28T00:30:00Z", "Pacific/Tongatapu")]
    public void today_at_the_place_holds_now(string now, string zone)
    {
        // Arrange
        var instant = DateTimeOffset.Parse(now, CultureInfo.InvariantCulture);

        // Act
        var today = ZonedDate.TodayAt(TimeZoneId.Of(zone), instant);

        // Assert
        today.Contains(instant).ShouldBeTrue();
    }

    [Test]
    public void a_day_starts_at_local_midnight()
    {
        // Act
        var start = new ZonedDate(new DateOnly(2026, 10, 27), Chicago).Start;

        // Assert
        start.ShouldBe(new DateTimeOffset(2026, 10, 27, 5, 0, 0, TimeSpan.Zero));
    }

    [Test]
    public void a_day_whose_midnight_falls_in_the_spring_gap_starts_at_the_end_of_the_gap()
    {
        // Act
        var start = new ZonedDate(new DateOnly(2026, 9, 6), Santiago).Start;

        // Assert
        start.ShouldBe(new DateTimeOffset(2026, 9, 6, 1, 0, 0, TimeSpan.FromHours(-3)));
    }

    [Test]
    public void a_day_whose_midnight_falls_in_the_spring_gap_lasts_23_hours()
    {
        // Arrange
        var day = new ZonedDate(new DateOnly(2026, 9, 6), Santiago);

        // Act
        var length = day.End - day.Start;

        // Assert
        length.ShouldBe(TimeSpan.FromHours(23));
    }

    [Test]
    public void a_day_whose_midnight_falls_in_a_gap_that_dotnet_does_not_mark_invalid_starts_at_the_end_of_the_gap()
    {
        // Asuncion moved from UTC-4 to UTC-3 at local midnight on October 6, 2024. IsInvalidTime gives false for 00:00.
        // Act
        var start = new ZonedDate(new DateOnly(2024, 10, 6), TimeZoneId.Of("America/Asuncion")).Start;

        // Assert
        start.ShouldBe(new DateTimeOffset(2024, 10, 6, 1, 0, 0, TimeSpan.FromHours(-3)));
    }

    [Test]
    public void a_date_that_the_zone_skipped_is_empty()
    {
        // Samoa moved from UTC-10 to UTC+14 at the end of December 29, 2011, so December 30 did not happen there.
        // Arrange
        var day = new ZonedDate(new DateOnly(2011, 12, 30), TimeZoneId.Of("Pacific/Apia"));

        // Assert
        day.Start.ShouldBe(new DateTimeOffset(2011, 12, 31, 0, 0, 0, TimeSpan.FromHours(14)));
        day.End.ShouldBe(day.Start);
    }

    [Test]
    public void a_day_holds_its_start_and_not_its_end()
    {
        // Arrange
        var day = new ZonedDate(new DateOnly(2026, 10, 27), Chicago);

        // Assert
        day.Contains(day.Start).ShouldBeTrue();
        day.Contains(day.End.AddTicks(-1)).ShouldBeTrue();
        day.Contains(day.End).ShouldBeFalse();
        day.End.ShouldBe(new ZonedDate(new DateOnly(2026, 10, 28), Chicago).Start);
    }
}
