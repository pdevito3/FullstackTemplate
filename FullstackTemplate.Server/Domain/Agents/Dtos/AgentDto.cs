namespace FullstackTemplate.Server.Domain.Agents.Dtos;

using Users;

public sealed record AgentDto
{
    public string Id { get; init; } = default!;
    public uint Version { get; init; }
    public string TenantId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string ClientId { get; init; } = default!;

    /// <summary>"delegated" or "machine".</summary>
    [NamesOf<AgentMode>]
    public string Mode { get; init; } = default!;

    /// <summary>The grant: the most that the agent can hold.</summary>
    [NamesOf<Permission>]
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
