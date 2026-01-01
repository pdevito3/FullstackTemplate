namespace FullstackTemplate.Server.Domain.Tenants.Dtos;

public sealed record TenantForCreationDto
{
    public string Name { get; init; } = default!;
}
