namespace FullstackTemplate.SharedTestHelpers.Examples.Fakes;

using Bogus;
using FullstackTemplate.Server.Domain.TimeZones;
using FullstackTemplate.Server.Examples.Workshops;

public static class WorkshopRules
{
    public static string Title(Faker f) => $"{f.Hacker.Adjective()} {f.Hacker.Noun()} workshop";

    public static int Capacity(Faker f) => f.Random.Int(1, Workshop.CapacityMaximum);

    public static DateTimeOffset StartsAt(TimeZoneId zone, DateTimeOffset now)
        => zone.ToInstant(ZonedDate.TodayAt(zone, now).Date.AddDays(7), new TimeOnly(10, 0))!.Value;
}
