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
    /// <summary>The roles of the user. The user holds the union of their permissions, with the permissions that someone added or removed by hand.</summary>
    [NamesOf<UserRole>]
    public IReadOnlyList<string> Roles { get; init; } = [];
    [NamesOf<Permission>]
    public IReadOnlyList<string> Permissions { get; init; } = [];
    /// <summary>The actions that the caller can send for this user now. A button in the SPA reads this list.</summary>
    [NamesOf<UserAction>]
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
    public uint Version { get; init; }
}
