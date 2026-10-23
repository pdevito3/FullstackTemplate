namespace FullstackTemplate.Server.Domain.Agents.Models;

public sealed record AgentForCreation
{
    /// <summary>A fixed ID with the prefix of the entity. When it is null, the entity makes a new ID.</summary>
    public string? Id { get; init; }
    public string TenantId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string ClientId { get; init; } = default!;
    public string Mode { get; init; } = default!;

    /// <summary>The first grant. Each name goes through <see cref="Agent.Grant"/>.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
