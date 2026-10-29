namespace FullstackTemplate.UnitTests.Domain.TimeZones;

using FullstackTemplate.Server.Domain.TimeZones;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

/// <summary>
/// The expected values come from the 2026 rules of the zones. Chicago moves to CDT (UTC-5) at 02:00 on March 8 and back
/// to CST (UTC-6) at 02:00 on November 1.
/// </summary>
public class TimeZoneIdTests
{
    private static readonly TimeZoneId Chicago = TimeZoneId.Of("America/Chicago");

    [Test]
    [Arguments("America/Chicago")]
    [Arguments("Asia/Calcutta")]
    [Arguments("UTC")]
    public void an_iana_id_is_accepted(string value)
    {
        // Act
        var zone = TimeZoneId.Of(value);

        // Assert
        zone.Value.ShouldBe(value);
    }

    [Test]
    public void an_alias_id_is_accepted_after_a_lookup_of_its_wrong_case()
    {
        // Arrange: macOS keeps the case of the first lookup as the ID of the zone, and Linux refuses the wrong case
        try { TimeZoneId.Of("asia/ulan_bator"); } catch (ValidationException) { }

        // Act
        var zone = TimeZoneId.Of("Asia/Ulan_Bator");

        // Assert
        zone.Value.ShouldBe("Asia/Ulan_Bator");
    }

    [Test]
    [Arguments("Central Standard Time")]
    [Arguments("america/chicago")]
    [Arguments("Mars/Olympus_Mons")]
    [Arguments("")]
    public void an_id_that_is_not_an_iana_zone_is_refused_on_the_time_zone_field(string value)
    {
        // Act
        var act = () => TimeZoneId.Of(value);

        // Assert
        act.ShouldThrow<ValidationException>().Property.ShouldBe("timeZone");
    }

    [Test]
    public void an_id_that_is_too_long_is_refused_with_the_limit()
    {
        // Act
        var act = () => TimeZoneId.Of("America/" + new string('A', TimeZoneId.MaxLength));

        // Assert
        var exception = act.ShouldThrow<ValidationException>();
        exception.Property.ShouldBe("timeZone");
        exception.Message.ShouldContain("100");
    }

    [Test]
    public void zones_with_the_same_id_are_equal()
    {
        // Act & Assert
        TimeZoneId.Of("America/Chicago").ShouldBe(Chicago);
        TimeZoneId.Of("America/Denver").ShouldNotBe(Chicago);
    }

    [Test]
    public void an_instant_shows_the_wall_clock_time_of_the_place()
    {
        // Act
        var local = Chicago.ToLocal(new DateTimeOffset(2026, 10, 28, 3, 30, 0, TimeSpan.Zero));

        // Assert
        local.Date.ShouldBe(new DateOnly(2026, 10, 27));
        local.Time.ShouldBe(new TimeOnly(22, 30));
    }

    [Test]
    [Arguments(7, 1, 14)]
    [Arguments(1, 15, 15)]
    public void a_wall_clock_time_converts_with_the_offset_of_its_season(int month, int day, int expectedUtcHour)
    {
        // Act
        var instant = Chicago.ToInstant(new DateOnly(2026, month, day), new TimeOnly(9, 0));

        // Assert
        instant.ShouldBe(new DateTimeOffset(2026, month, day, expectedUtcHour, 0, 0, TimeSpan.Zero));
    }

    [Test]
    public void a_time_in_the_spring_gap_does_not_exist()
    {
        // Act
        var instant = Chicago.ToInstant(new DateOnly(2026, 3, 8), new TimeOnly(2, 30));

        // Assert
        instant.ShouldBeNull();
    }

    [Test]
    public void a_time_in_the_repeated_fall_hour_is_the_first_one()
    {
        // Act
        var instant = Chicago.ToInstant(new DateOnly(2026, 11, 1), new TimeOnly(1, 30));

        // Assert
        instant.ShouldBe(new DateTimeOffset(2026, 11, 1, 6, 30, 0, TimeSpan.Zero));
    }
}
