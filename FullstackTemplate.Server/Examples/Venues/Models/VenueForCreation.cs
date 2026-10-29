namespace FullstackTemplate.Server.Examples.Venues.Models;

public sealed record VenueForCreation
{
    /// <summary>A fixed ID with the prefix of the entity. When it is null, the entity makes a new ID.</summary>
    public string? Id { get; init; }
    public string TenantId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string TimeZone { get; init; } = default!;
}
