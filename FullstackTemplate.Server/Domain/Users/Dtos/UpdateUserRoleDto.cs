namespace FullstackTemplate.Server.Domain.Users.Dtos;

using Resources;

public sealed record UpdateUserRoleDto
{
    [RequiredField]
    [NamesOf<UserRole>]
    public string Role { get; init; } = default!;
}
