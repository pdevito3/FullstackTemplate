namespace FullstackTemplate.Server.Domain.Invites.Dtos;

using Users;

/// <summary>What the join page shows before the claim.</summary>
public sealed record InvitePreviewDto
{
    public string TenantName { get; init; } = default!;
    [NamesOf<UserRole>]
    public string Role { get; init; } = default!;
    public DateTimeOffset ExpiresAt { get; init; }
    /// <summary>When true, only a caller whose token has the email of the invite can claim it.</summary>
    public bool RequireEmailMatch { get; init; }
}
