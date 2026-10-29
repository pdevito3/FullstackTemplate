namespace FullstackTemplate.Server.Examples.Workshops.Dtos;

public sealed record WorkshopForSchedulingDto
{
    /// <summary>The version of the workshop that the caller loaded. A stale version gives 409.</summary>
    public required uint Version { get; init; }
}
