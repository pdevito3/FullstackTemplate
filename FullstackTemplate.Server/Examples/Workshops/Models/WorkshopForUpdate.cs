namespace FullstackTemplate.Server.Examples.Workshops.Models;

public sealed record WorkshopForUpdate
{
    public string Title { get; init; } = default!;
    public DateTimeOffset StartsAt { get; init; }
    public int Capacity { get; init; }
    public uint Version { get; init; }
}
