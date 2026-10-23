namespace FullstackTemplate.Server.Domain.Agents.Dtos;

public sealed record AgentDto
{
    public string Id { get; init; } = default!;
    public string TenantId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string ClientId { get; init; } = default!;

    /// <summary>"delegated" or "machine".</summary>
    public string Mode { get; init; } = default!;

    /// <summary>The grant: the most that the agent can hold.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
