namespace FullstackTemplate.Server.Domain.Users.Dtos;

public sealed record UserForUpdateDto
{
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Username { get; init; } = default!;
}
