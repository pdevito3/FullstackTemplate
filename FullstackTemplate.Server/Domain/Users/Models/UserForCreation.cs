namespace FullstackTemplate.Server.Domain.Users.Models;

public sealed record UserForCreation
{
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public string Identifier { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Username { get; init; } = default!;
    public string Role { get; init; } = default!;
}
