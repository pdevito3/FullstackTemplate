namespace FullstackTemplate.Server.Domain.Invites.Dtos;

using Users;

/// <summary>An invite as an admin sees it. It never has the token (INV-2).</summary>
public sealed record InviteDto
{
    public string Id { get; init; } = default!;
    public string TenantId { get; init; } = default!;
    [NamesOf<UserRole>]
    public string Role { get; init; } = default!;
    /// <summary>The email that the invite is for. Null when the admin gave none.</summary>
    public string? Email { get; init; }
    public bool RequireEmailMatch { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    /// <summary>The stored state. A pending invite after <see cref="ExpiresAt"/> is expired.</summary>
    [NamesOf<InviteStatus>]
    public string Status { get; init; } = default!;
    /// <summary>True when a pending invite is past <see cref="ExpiresAt"/>. The state on the screen reads this flag.</summary>
    public bool IsExpired { get; init; }
    /// <summary>The user that the claim added. Null until a claim.</summary>
    public string? ClaimedByUserId { get; init; }
    /// <summary>The actions that the caller can send for this invite now. A button in the SPA reads this list.</summary>
    [NamesOf<InviteAction>]
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
    public uint Version { get; init; }
}
