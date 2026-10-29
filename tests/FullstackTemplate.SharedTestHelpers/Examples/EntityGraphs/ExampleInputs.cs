namespace FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;

public sealed class VenueInput
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? TimeZone { get; set; }
}

public sealed class WorkshopInput
{
    public string? Id { get; set; }

    public string? Title { get; set; }

    public DateTimeOffset? StartsAt { get; set; }

    public int? Capacity { get; set; }

    /// <summary>
    /// The <c>usr_</c> ID of the member who added the workshop. It has no derive: null is a workshop that an agent or
    /// system work added.
    /// </summary>
    public string? OrganizerId { get; set; }
}
