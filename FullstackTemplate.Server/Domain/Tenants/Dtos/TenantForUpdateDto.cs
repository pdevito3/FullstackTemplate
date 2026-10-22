namespace FullstackTemplate.Server.Domain.Tenants.Dtos;

using Resources;

public sealed record TenantForUpdateDto
{
    [RequiredField]
    public string Name { get; init; } = default!;
    /// <summary>The version of the tenant that the caller loaded. A stale version gives 409.</summary>
    public required uint Version { get; init; }
}
