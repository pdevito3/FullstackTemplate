namespace FullstackTemplate.Server.Examples.Workshops.Dtos;

using Resources;

public sealed record WorkshopForCreationDto
{
    [RequiredField]
    public string VenueId { get; init; } = default!;

    [RequiredField]
    [SchemaMaxLength(typeof(Workshop), nameof(Workshop.TitleMaxLength))]
    public string Title { get; init; } = default!;

    public required DateTimeOffset StartsAt { get; init; }

    [SchemaMaximum(typeof(Workshop), nameof(Workshop.CapacityMaximum))]
    public required int Capacity { get; init; }
}
