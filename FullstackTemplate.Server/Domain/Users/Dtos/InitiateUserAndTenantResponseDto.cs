namespace FullstackTemplate.Server.Domain.Users.Dtos;

using Tenants.Dtos;

public sealed record InitiateUserAndTenantResponseDto
{
    public TenantDto Tenant { get; init; } = default!;
    public UserDto User { get; init; } = default!;
}
