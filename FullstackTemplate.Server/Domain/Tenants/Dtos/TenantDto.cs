namespace FullstackTemplate.Server.Domain.Tenants.Dtos;

public sealed record TenantDto
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public uint Version { get; init; }
}
