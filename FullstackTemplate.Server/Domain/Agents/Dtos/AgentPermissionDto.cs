namespace FullstackTemplate.Server.Domain.Agents.Dtos;

using Resources;

public sealed record AgentPermissionDto
{
    [RequiredField]
    public string Permission { get; init; } = default!;
}
