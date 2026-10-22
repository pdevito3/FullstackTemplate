namespace FullstackTemplate.Server.Domain.Users.Dtos;

public sealed record UserDto
{
    public string Id { get; init; } = default!;
    public string TenantId { get; init; } = default!;
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public string Identifier { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Username { get; init; } = default!;
    public string Role { get; init; } = default!;
    public IReadOnlyList<string> Permissions { get; init; } = [];
    public uint Version { get; init; }
}
