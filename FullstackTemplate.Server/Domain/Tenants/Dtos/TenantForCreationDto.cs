namespace FullstackTemplate.Server.Domain.Tenants.Dtos;

using Resources;

public sealed record TenantForCreationDto
{
    [RequiredField]
    [SchemaMaxLength(typeof(Tenant), nameof(Tenant.NameMaxLength))]
    public string Name { get; init; } = default!;
}
