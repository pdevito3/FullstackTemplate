namespace FullstackTemplate.SharedTestHelpers.Examples.Fakes;

using Bogus;

/// <summary>The one definition of a realistic venue. Each method is named for the property that it fills.</summary>
public static class VenueRules
{
    // Zones on both sides of UTC, so a test that takes today from the UTC date fails on most days
    private static readonly string[] TimeZones =
        ["Pacific/Tongatapu", "Asia/Tokyo", "Europe/Berlin", "America/Chicago", "America/Los_Angeles", "Pacific/Honolulu"];

    public static string Name(Faker f) => $"{f.Address.City()} {f.PickRandom("Hall", "Studio", "Library", "Loft")}";

    public static string TimeZone(Faker f) => f.PickRandom(TimeZones);
}
