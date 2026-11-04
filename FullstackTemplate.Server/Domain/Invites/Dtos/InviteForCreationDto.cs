namespace FullstackTemplate.Server.Domain.Invites.Dtos;

using EmailAddresses;
using Resources;
using Users;

public sealed record InviteForCreationDto
{
    /// <summary>
    /// Only for a caller that sees all tenants, such as an allow-listed machine client. That caller must name the tenant.
    /// A user always invites to their own tenant. If a user sends a tenant, it must be their own tenant.
    /// </summary>
    public string? TenantId { get; init; }

    [RequiredField]
    [NamesOf<UserRole>]
    public string Role { get; init; } = default!;

    /// <summary>The email of the colleague. The invite requires it only when <see cref="RequireEmailMatch"/> is true.</summary>
    [SchemaMaxLength(typeof(EmailAddress), nameof(EmailAddress.MaxLength))]
    public string? Email { get; init; }

    /// <summary>When true, only a caller whose token has <see cref="Email"/> can claim the invite.</summary>
    public required bool RequireEmailMatch { get; init; }
}
