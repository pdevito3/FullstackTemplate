namespace FullstackTemplate.Server.Domain.Users.Dtos;

using Resources;

public sealed record UpdateUserRoleDto
{
    [RequiredField]
    public string Role { get; init; } = default!;
}
