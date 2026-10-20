namespace FullstackTemplate.Server.Domain.Users.Dtos;

using Resources;

public sealed record UserPermissionDto
{
    [RequiredField]
    public string Permission { get; init; } = default!;
}
