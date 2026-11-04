namespace FullstackTemplate.Server.Domain.Invites.Models;

public sealed record InviteForCreation
{
    /// <summary>A fixed ID with the prefix of the entity. When it is null, the entity makes a new ID.</summary>
    public string? Id { get; init; }
    public string TenantId { get; init; } = default!;
    public string Role { get; init; } = default!;
    public string? Email { get; init; }
    public bool RequireEmailMatch { get; init; }
}
