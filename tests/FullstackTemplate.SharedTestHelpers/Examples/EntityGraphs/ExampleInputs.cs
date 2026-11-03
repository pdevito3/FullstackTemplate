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

    public string? OrganizerId { get; set; }
}

public sealed class WorkshopAttachmentInput
{
    public string? Id { get; set; }

    public string? FileName { get; set; }

    public string? ClaimedMediaType { get; set; }

    /// <summary>The first bytes of the file, such as a sample of <c>SampleFiles</c>. They decide its type.</summary>
    public byte[]? Head { get; set; }

    public long? SizeBytes { get; set; }

    public DateTimeOffset? UploadedAt { get; set; }
}
