namespace FullstackTemplate.Server.Examples.Venues.Dtos;

public sealed record VenueDto
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    /// <summary>The IANA time zone of the venue, for example America/Chicago.</summary>
    public string TimeZone { get; init; } = default!;
    public uint Version { get; init; }
}
