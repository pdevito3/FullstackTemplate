namespace FullstackTemplate.Server.Domain.Users.Dtos;

using Resources;

public sealed record UserPermissionDto
{
    [RequiredField]
    [NamesOf<Permission>]
    public string Permission { get; init; } = default!;
}
