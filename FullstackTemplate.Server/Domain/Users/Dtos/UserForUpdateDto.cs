namespace FullstackTemplate.Server.Domain.Users.Dtos;

using EmailAddresses;
using Resources;

public sealed record UserForUpdateDto
{
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
    /// <summary>The version of the user that the caller loaded. A stale version gives 409.</summary>
    public required uint Version { get; init; }
}
