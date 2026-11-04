namespace FullstackTemplate.Server.Domain.Invites;

using DomainEvents;
using EmailAddresses;
using Exceptions;
using Models;
using Tenants;
using Users;
using Users.Models;

/// <summary>
/// A one-use link that adds a colleague to a tenant with a role. A claim binds the <c>sub</c> of the caller to a new
/// user record, so the app never creates an account in the auth provider. See <c>docs/specs/user-invites.md</c>.
/// </summary>
public class Invite : BaseEntity, ITenantable
{
    public const string IdPrefix = "inv";
    public const string NotValidMessage = "This invite link is not valid. Ask a tenant admin for a new invite.";
    public const string ExpiredMessage = "This invite expired. Ask a tenant admin for a new invite.";
    public const string UsedOrRevokedMessage = "This invite was used or revoked. Ask a tenant admin for a new invite.";
    public const string EmailMismatchMessage =
        "This invite is for another email address. Sign in with the account of that email.";
    public const string EmailNotVerifiedMessage =
        "The email address of your account is not verified. Verify it, then open the invite link again.";
    public const string CallerHasTenantMessage = "Your account already belongs to a tenant.";

    /// <summary>The time from the creation to the expiry. It is not a setting (spec section 8).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    [NotSensitive] public string TenantId { get; private set; } = default!;
    [NotSensitive] public UserRole Role { get; private set; } = default!;
    [Sensitive] public EmailAddress? Email { get; private set; }
    public bool RequireEmailMatch { get; private set; }
    [Sensitive] public string TokenHash { get; private set; } = default!;
    [NotSensitive] public DateTimeOffset ExpiresAt { get; private set; }
    [NotSensitive] public InviteStatus Status { get; private set; } = default!;
    public string? ClaimedByUserId { get; private set; }

    public static Invite Create(InviteForCreation inviteForCreation, InviteToken token, DateTimeOffset now)
    {
        var invite = new Invite
        {
            TenantId = inviteForCreation.TenantId,
            Role = UserRole.Of(inviteForCreation.Role),
            Email = string.IsNullOrWhiteSpace(inviteForCreation.Email)
                ? null
                : EmailAddress.Of(inviteForCreation.Email.Trim()),
            RequireEmailMatch = inviteForCreation.RequireEmailMatch,
            TokenHash = token.Hash,
            ExpiresAt = now.ToUniversalTime() + Lifetime,
            Status = InviteStatus.Pending(),
        };
        if (inviteForCreation.Id is not null)
            invite.SetId(inviteForCreation.Id);

        ValidationException.Must(EntityId.IsValid(invite.TenantId, Tenant.IdPrefix), "tenantId", "Please provide a valid tenant ID.");
        ValidationException.MustNot(invite.RequireEmailMatch && invite.Email is null, "email",
            "Please give the email that the invite is for, or do not require an email match.");
        invite.QueueDomainEvent(new InviteCreated(invite.Id));

        return invite;
    }

    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>The check of a preview and of a claim: the invite is pending and not expired.</summary>
    /// <exception cref="GoneException">The invite was claimed or revoked, or it expired.</exception>
    public void GuardClaimable(DateTimeOffset now)
    {
        if (Status != InviteStatus.Pending())
            throw new GoneException(UsedOrRevokedMessage);
        if (IsExpiredAt(now))
            throw new GoneException(ExpiredMessage);
    }

    /// <summary>
    /// Adds the user of the claim to the tenant of the invite, with the role of the invite. Save the user and the
    /// invite in one save, so the version check of the invite stops a second claim that raced this one.
    /// </summary>
    /// <exception cref="GoneException">The invite was claimed or revoked, or it expired.</exception>
    /// <exception cref="ForbiddenAccessException">
    /// The invite requires an email match, and the token email differs or the provider did not verify it.
    /// </exception>
    public User Claim(InviteClaim claim, DateTimeOffset now)
    {
        GuardClaimable(now);
        GuardEmailMatch(claim);

        var user = User.Create(new UserForCreation
        {
            TenantId = TenantId,
            FirstName = claim.FirstName,
            LastName = claim.LastName,
            Identifier = claim.Identifier,
            Email = claim.Email,
            Username = claim.Username,
            Role = Role.Value,
        });
        ClaimedByUserId = user.Id;
        MoveTo(InviteStatus.Claimed());

        return user;
    }

    /// <summary>
    /// The email match of INV-9. A provider that sends no <c>email_verified</c> fails the match, because an email that
    /// the provider did not verify can be the email of anyone.
    /// </summary>
    private void GuardEmailMatch(InviteClaim claim)
    {
        if (!RequireEmailMatch)
            return;
        if (!string.Equals(Email?.Value, claim.TokenEmail?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenAccessException(EmailMismatchMessage);
        if (!claim.TokenEmailVerified)
            throw new ForbiddenAccessException(EmailNotVerifiedMessage);
    }

    /// <summary>A revoke of a revoked invite changes nothing, so a run again after its own commit succeeds.</summary>
    /// <exception cref="ValidationException">The invite was claimed.</exception>
    public Invite Revoke() => MoveTo(InviteStatus.Revoked());

    public IReadOnlyList<InviteAction> AllowedActions(Caller caller) => InviteAction.AllowedFor(caller, Status);

    private Invite MoveTo(InviteStatus target)
    {
        if (Status == target)
            return this;

        Status = Status.MoveTo(target);
        QueueDomainEvent(new InviteStatusChanged(Id, Status.Value));

        return this;
    }

    protected Invite() : base(IdPrefix) { } // For EF Core
}
