namespace FullstackTemplate.Server.Examples.Workshops.Dtos;

using Domain;

public sealed record WorkshopDto
{
    public string Id { get; init; } = default!;
    public string VenueId { get; init; } = default!;
    public string Title { get; init; } = default!;
    public DateTimeOffset StartsAt { get; init; }
    /// <summary>The IANA time zone of the venue, for example America/Chicago. The SPA shows the start in this zone.</summary>
    public string VenueTimeZone { get; init; } = default!;
    public int Capacity { get; init; }
    [NamesOf<WorkshopStatus>]
    public string Status { get; init; } = default!;
    /// <summary>The actions that the caller can send for this workshop now. A button in the SPA reads this list.</summary>
    [NamesOf<WorkshopAction>]
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
    public uint Version { get; init; }
}
