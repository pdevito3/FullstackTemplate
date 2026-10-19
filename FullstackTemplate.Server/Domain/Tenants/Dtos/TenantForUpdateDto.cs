namespace FullstackTemplate.Server.Domain.Tenants.Dtos;

public sealed record TenantForUpdateDto
{
    public string Name { get; init; } = default!;
}
