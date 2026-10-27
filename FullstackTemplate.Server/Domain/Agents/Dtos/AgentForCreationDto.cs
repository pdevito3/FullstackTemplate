namespace FullstackTemplate.Server.Domain.Agents.Dtos;

using Resources;
using Users;

public sealed record AgentForCreationDto
{
    /// <summary>
    /// Only for a caller that sees all tenants. That caller must name the tenant. A tenant admin always adds to their
    /// own tenant. If a tenant admin sends a tenant, it must be their own tenant.
    /// </summary>
    public string? TenantId { get; init; }
    [RequiredField]
    public string Name { get; init; } = default!;

    /// <summary>The client ID in the tokens of the client. Case-sensitive.</summary>
    [RequiredField]
    public string ClientId { get; init; } = default!;

    /// <summary>
    /// "delegated" for a client that acts for a signed-in user, or "machine" for a client that acts for itself with
    /// client credentials. Only a caller that manages all tenants can add a machine agent.
    /// </summary>
    [RequiredField]
    [NamesOf<AgentMode>]
    public string Mode { get; init; } = default!;

    /// <summary>The first grant. The caller must hold each permission. No permission when it is not sent.</summary>
    [NamesOf<Permission>]
    public IReadOnlyList<string>? Permissions { get; init; }
}
