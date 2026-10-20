namespace FullstackTemplate.Server.Domain.Users.Dtos;

using Resources;

public sealed record InitiateUserAndTenantDto
{
    [RequiredField]
    public string TenantName { get; init; } = default!;
    [RequiredField]
    public string FirstName { get; init; } = default!;
    [RequiredField]
    public string LastName { get; init; } = default!;
    [RequiredField]
    public string Identifier { get; init; } = default!;
    [RequiredField]
    public string Email { get; init; } = default!;
    [RequiredField]
    public string Username { get; init; } = default!;
    [RequiredField]
    public string Role { get; init; } = default!;
}
