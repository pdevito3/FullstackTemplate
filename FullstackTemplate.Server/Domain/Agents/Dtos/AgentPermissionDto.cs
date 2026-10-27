namespace FullstackTemplate.Server.Domain.Agents.Dtos;

using Resources;
using Users;

public sealed record AgentPermissionDto
{
    [RequiredField]
    [NamesOf<Permission>]
    public string Permission { get; init; } = default!;
}
