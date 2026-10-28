namespace FullstackTemplate.Server.Examples.Workshops.Dtos;

using Resources;

public sealed record WorkshopForUpdateDto
{
    [RequiredField]
    [SchemaMaxLength(typeof(Workshop), nameof(Workshop.TitleMaxLength))]
    public string Title { get; init; } = default!;

    public required DateTimeOffset StartsAt { get; init; }

    [SchemaMaximum(typeof(Workshop), nameof(Workshop.CapacityMaximum))]
    public required int Capacity { get; init; }

    /// <summary>The version of the workshop that the caller loaded. A stale version gives 409.</summary>
    public required uint Version { get; init; }
}
