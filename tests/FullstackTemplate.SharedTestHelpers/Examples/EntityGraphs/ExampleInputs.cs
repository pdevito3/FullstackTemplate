namespace FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;

public sealed class VenueInput
{
    /// <summary>A <c>ven_</c> ID. Null by default, so the entity makes the ID. Set it to give a fixed ID.</summary>
    public string? Id { get; set; }

    public string? Name { get; set; }

    /// <summary>An IANA time zone, for example "America/Chicago".</summary>
    public string? TimeZone { get; set; }
}

public sealed class WorkshopInput
{
    /// <summary>A <c>wks_</c> ID. Null by default, so the entity makes the ID. Set it to give a fixed ID.</summary>
    public string? Id { get; set; }

    public string? Title { get; set; }

    /// <summary>The default is a start that the schedule rules offer: a week after today, at 10:00 at the venue.</summary>
    public DateTimeOffset? StartsAt { get; set; }

    public int? Capacity { get; set; }
}
