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

    /// <summary>
    /// Chromium 156 and Node 24 give these old names of zones, which IANA keeps as links in its file "backward". Ubuntu
    /// 26.04 puts the files of the links in the package tzdata-legacy, so a host can have no file for them.
    /// </summary>
    [Test]
    [Arguments("Africa/Asmera")]
    [Arguments("America/Buenos_Aires")]
    [Arguments("America/Catamarca")]
    [Arguments("America/Cordoba")]
    [Arguments("America/Godthab")]
    [Arguments("America/Indianapolis")]
    [Arguments("America/Jujuy")]
    [Arguments("America/Louisville")]
    [Arguments("America/Mendoza")]
    [Arguments("Asia/Calcutta")]
    [Arguments("Asia/Katmandu")]
    [Arguments("Asia/Rangoon")]
    [Arguments("Asia/Saigon")]
    [Arguments("Atlantic/Faeroe")]
    [Arguments("Europe/Kiev")]
    [Arguments("Pacific/Enderbury")]
    [Arguments("Pacific/Ponape")]
    [Arguments("Pacific/Truk")]
    public void an_old_name_that_a_browser_gives_is_accepted(string value)
    {
        // Act
        var zone = TimeZoneId.Of(value);

        // Assert
        zone.Value.ShouldBe(value);
    }

    public static IEnumerable<(string, string)> Links => TimeZoneLinks.All.Select(link => (link.Name, link.Target));

    [Test]
    public void the_link_table_holds_the_links_of_the_iana_file()
    {
        // Arrange: an empty table gives no rows to the tests of each link, and they pass with no check

        // Act
        var links = TimeZoneLinks.All;

        // Assert
        links.ShouldContain(("Asia/Calcutta", "Asia/Kolkata"));
        links.ShouldContain(("Asia/Ulan_Bator", "Asia/Ulaanbaatar"));
    }

    [Test]
    [MethodDataSource(nameof(Links))]
    public void each_link_of_the_iana_database_shows_the_time_of_the_zone_that_it_names(string name, string target)
    {
        // Arrange
        var winter = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var summer = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        var link = TimeZoneId.Of(name);
        var zone = TimeZoneId.Of(target);

        // Assert
        link.Value.ShouldBe(name);
        link.ToLocal(winter).ShouldBe(zone.ToLocal(winter));
        link.ToLocal(summer).ShouldBe(zone.ToLocal(summer));
    }

    [Test]
    public void a_wrong_case_of_an_old_name_is_refused()
    {
        // Act
        var act = () => TimeZoneId.Of("asia/calcutta");

        // Assert
        act.ShouldThrow<ValidationException>().Property.ShouldBe("timeZone");
    }

    [Test]
    public void an_id_is_accepted_after_a_lookup_of_its_wrong_case()
    {
        // Arrange: macOS keeps the case of the first lookup as the ID of the zone, and Linux refuses the wrong case.
        // Etc/GMT+5 is in neither map of wrong cases on macOS, so this lookup reaches the host.
        try { TimeZoneId.Of("etc/gmt+5"); } catch (ValidationException) { }

        // Act
        var zone = TimeZoneId.Of("Etc/GMT+5");

        // Assert
        zone.Value.ShouldBe("Etc/GMT+5");
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
    public void a_wrong_case_of_a_zone_is_refused_as_not_known_not_as_not_a_place()
    {
        // Act
        var act = () => TimeZoneId.Of("america/chicago");

        // Assert
        act.ShouldThrow<ValidationException>().Message.ShouldContain("is not a time zone that the server knows");
    }

    [Test]
    public void the_zone_of_an_unknown_local_time_is_refused_as_not_a_place()
    {
        // Act
        var act = () => TimeZoneId.Of("Factory");

        // Assert
        var exception = act.ShouldThrow<ValidationException>();
        exception.Property.ShouldBe("timeZone");
        exception.Message.ShouldContain("is not the time zone of a place");
    }

    // A host with the file of the ID refuses it as not a place. A host with no file refuses it as not known.
    [Test]
    [Arguments("FACTORY")]
    [Arguments("factory")]
    [Arguments("posixrules")]
    [Arguments("localtime")]
    [Arguments("right/America/Chicago")]
    [Arguments("Posixrules")]
    [Arguments("POSIXRULES")]
    [Arguments("Localtime")]
    [Arguments("Right/America/Chicago")]
    public void a_tzdata_file_that_is_not_the_zone_of_a_place_is_refused(string value)
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
