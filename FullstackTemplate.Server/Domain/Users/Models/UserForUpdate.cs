namespace FullstackTemplate.Server.Domain.Users.Models;

public sealed record UserForUpdate
{
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Username { get; init; } = default!;
}
