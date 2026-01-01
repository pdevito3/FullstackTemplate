namespace FullstackTemplate.Server.Domain.Tenants.Dtos;

public sealed record TenantDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
}
