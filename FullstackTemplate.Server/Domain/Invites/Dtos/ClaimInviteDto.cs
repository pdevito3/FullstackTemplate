namespace FullstackTemplate.Server.Domain.Invites.Dtos;

using EmailAddresses;
using Resources;
using Users;

/// <summary>The token and the values of the new user. The join page fills the values from the token claims (INV-8).</summary>
public sealed record ClaimInviteDto
{
    [RequiredField]
    [SchemaMaxLength(typeof(InviteToken), nameof(InviteToken.Length))]
    public string Token { get; init; } = default!;
    [RequiredField]
    [SchemaMaxLength(typeof(User), nameof(User.FirstNameMaxLength))]
    public string FirstName { get; init; } = default!;
    [RequiredField]
    [SchemaMaxLength(typeof(User), nameof(User.LastNameMaxLength))]
    public string LastName { get; init; } = default!;
    [RequiredField]
    [SchemaMaxLength(typeof(EmailAddress), nameof(EmailAddress.MaxLength))]
    public string Email { get; init; } = default!;
    [RequiredField]
    [SchemaMaxLength(typeof(User), nameof(User.UsernameMaxLength))]
    public string Username { get; init; } = default!;
}
