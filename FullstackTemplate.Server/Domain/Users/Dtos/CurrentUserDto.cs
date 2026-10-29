namespace FullstackTemplate.Server.Domain.Users.Dtos;

using Resources;

/// <summary>The signed-in caller as the Server sees it in the access token.</summary>
public sealed record CurrentUserDto
{
    /// <summary>The <c>sub</c> claim of the token.</summary>
    [RequiredField]
    public string Identifier { get; init; } = default!;

    /// <summary>The display name from the token, or null when the identity provider sends no name.</summary>
    public string? Name { get; init; }
}
