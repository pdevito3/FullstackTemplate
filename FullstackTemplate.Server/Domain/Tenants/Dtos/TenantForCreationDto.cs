namespace FullstackTemplate.Server.Domain.Tenants.Dtos;

using Resources;

public sealed record TenantForCreationDto
{
    [RequiredField]
    public string Name { get; init; } = default!;
}
