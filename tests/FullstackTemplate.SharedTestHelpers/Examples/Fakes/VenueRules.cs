namespace FullstackTemplate.SharedTestHelpers.Examples.Fakes;

using Bogus;

public static class VenueRules
{
    private static readonly string[] TimeZones =
        ["Pacific/Tongatapu", "Asia/Tokyo", "Europe/Berlin", "America/Chicago", "America/Los_Angeles", "Pacific/Honolulu"];

    public static string Name(Faker f) => $"{f.Address.City()} {f.PickRandom("Hall", "Studio", "Library", "Loft")}";

    public static string TimeZone(Faker f) => f.PickRandom(TimeZones);
}
