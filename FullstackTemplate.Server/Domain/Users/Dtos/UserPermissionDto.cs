namespace FullstackTemplate.Server.Domain.Users.Dtos;

public sealed record UserPermissionDto
{
    public string Permission { get; init; } = default!;
}
