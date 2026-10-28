namespace FullstackTemplate.SharedTestHelpers.Examples.Fakes;

using Bogus;
using FullstackTemplate.Server.Domain.TimeZones;
using FullstackTemplate.Server.Examples.Workshops;

/// <summary>The one definition of a realistic workshop. Each method is named for the property that it fills.</summary>
public static class WorkshopRules
{
    public static string Title(Faker f) => $"{f.Hacker.Adjective()} {f.Hacker.Noun()} workshop";

    public static int Capacity(Faker f) => f.Random.Int(1, Workshop.CapacityMaximum);

    /// <summary>A start that the schedule rules offer: a week after today, at 10:00 at the venue.</summary>
    public static DateTimeOffset StartsAt(TimeZoneId zone, DateTimeOffset now)
        => zone.ToInstant(ZonedDate.TodayAt(zone, now).Date.AddDays(7), new TimeOnly(10, 0))!.Value;
}
