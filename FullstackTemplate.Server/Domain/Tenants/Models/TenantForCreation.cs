namespace FullstackTemplate.Server.Domain.Tenants.Models;

public sealed record TenantForCreation
{
    public string Name { get; init; } = default!;
}
