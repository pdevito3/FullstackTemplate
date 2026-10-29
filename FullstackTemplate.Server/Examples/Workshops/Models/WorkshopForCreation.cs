namespace FullstackTemplate.Server.Examples.Workshops.Models;

public sealed record WorkshopForCreation
{
    /// <summary>A fixed ID with the prefix of the entity. When it is null, the entity makes a new ID.</summary>
    public string? Id { get; init; }
    public string Title { get; init; } = default!;
    public DateTimeOffset StartsAt { get; init; }
    public int Capacity { get; init; }
    /// <summary>The user ID of the member who adds the workshop. Null when another kind of caller adds it.</summary>
    public string? OrganizerId { get; init; }
}
