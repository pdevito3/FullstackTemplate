namespace FullstackTemplate.Server.Domain.Attachments.Dtos;

public sealed record FileTypeDto
{
    /// <summary>The name that the screen shows, for example PNG.</summary>
    public string Name { get; init; } = default!;
    public string MediaType { get; init; } = default!;
    /// <summary>The extensions of a file name of this type, with the dot, for example .jpg.</summary>
    public List<string> Extensions { get; init; } = [];
}
