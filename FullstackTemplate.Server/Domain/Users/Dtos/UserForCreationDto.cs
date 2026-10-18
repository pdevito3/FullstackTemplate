namespace FullstackTemplate.Server.Domain.Users.Dtos;

public sealed record UserForCreationDto
{
    /// <summary>
    /// Only for a caller that sees all tenants, such as an allow-listed machine client. That caller must name the tenant.
    /// A user always adds to their own tenant. If a user sends a tenant, it must be their own tenant.
    /// </summary>
    public string? TenantId { get; init; }
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public string Identifier { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Username { get; init; } = default!;
    public string Role { get; init; } = default!;
}
