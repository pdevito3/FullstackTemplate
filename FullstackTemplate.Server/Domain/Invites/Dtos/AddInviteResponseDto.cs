namespace FullstackTemplate.Server.Domain.Invites.Dtos;

/// <summary>The new invite and its token. This is the one response that has the token (INV-2).</summary>
public sealed record AddInviteResponseDto
{
    public InviteDto Invite { get; init; } = default!;
    /// <summary>The secret of the link. The SPA puts it in the fragment of the join URL: <c>/join#token=...</c>.</summary>
    public string Token { get; init; } = default!;
}
