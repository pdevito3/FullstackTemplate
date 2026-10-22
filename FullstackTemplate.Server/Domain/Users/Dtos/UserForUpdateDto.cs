namespace FullstackTemplate.Server.Domain.Users.Dtos;

using Resources;

public sealed record UserForUpdateDto
{
    [RequiredField]
    public string FirstName { get; init; } = default!;
    [RequiredField]
    public string LastName { get; init; } = default!;
    [RequiredField]
    public string Email { get; init; } = default!;
    [RequiredField]
    public string Username { get; init; } = default!;
    /// <summary>The version of the user that the caller loaded. A stale version gives 409.</summary>
    public required uint Version { get; init; }
}
