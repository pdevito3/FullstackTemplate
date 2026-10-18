namespace FullstackTemplate.Server.Domain.Tenants.Models;

public sealed record TenantForCreation
{
    /// <summary>A fixed ID with the prefix of the entity. When it is null, the entity makes a new ID.</summary>
    public string? Id { get; init; }
    public string Name { get; init; } = default!;
}
