namespace FullstackTemplate.Server.Domain.Tenants.Dtos;

using Resources;

public sealed record TenantForUpdateDto
{
    [RequiredField]
    public string Name { get; init; } = default!;
}
