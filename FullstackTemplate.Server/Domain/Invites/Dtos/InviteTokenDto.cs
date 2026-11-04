namespace FullstackTemplate.Server.Domain.Invites.Dtos;

using Resources;

/// <summary>The token of an invite link. It is in the body, so it is not in a log of the path or the query.</summary>
public sealed record InviteTokenDto
{
    [RequiredField]
    [SchemaMaxLength(typeof(InviteToken), nameof(InviteToken.Length))]
    public string Token { get; init; } = default!;
}
