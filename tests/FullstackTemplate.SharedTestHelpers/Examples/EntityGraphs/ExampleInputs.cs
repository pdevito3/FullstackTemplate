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
}
