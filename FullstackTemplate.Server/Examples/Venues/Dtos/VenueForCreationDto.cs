namespace FullstackTemplate.Server.Examples.Venues.Dtos;

using Domain.TimeZones;
using Resources;

public sealed record VenueForCreationDto
{
    [RequiredField]
    [SchemaMaxLength(typeof(Venue), nameof(Venue.NameMaxLength))]
    public string Name { get; init; } = default!;

    /// <summary>The IANA time zone of the venue, for example America/Chicago.</summary>
    [RequiredField]
    [SchemaMaxLength(typeof(TimeZoneId), nameof(TimeZoneId.MaxLength))]
    public string TimeZone { get; init; } = default!;
}
