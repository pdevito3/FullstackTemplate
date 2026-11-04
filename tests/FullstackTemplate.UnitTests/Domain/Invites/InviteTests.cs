namespace FullstackTemplate.UnitTests.Domain.Invites;

using FullstackTemplate.Server.Domain.Invites;
using FullstackTemplate.Server.Domain.Invites.DomainEvents;
using FullstackTemplate.Server.Domain.Invites.Models;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;

public class InviteTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private static Invite PendingInvite(Action<InviteInput>? customize = null)
    {
        TestGraph.Start(out _).AddTenant(out _).AddInvite(out var invite, i =>
        {
            i.CreatedAt = CreatedAt;
            customize?.Invoke(i);
        });
        return invite;
    }

    private static InviteClaim ClaimBy(string identifier, string? tokenEmail = null) => new()
    {
        Identifier = identifier,
        TokenEmail = tokenEmail,
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = "ada@example.com",
        Username = "ada",
    };

    [Test, Property("Rule", "INV-2")]
    public void create_stores_the_hash_of_the_token_and_never_the_token()
    {
        // Arrange
        var token = InviteToken.New();

        // Act
        TestGraph.Start(out _).AddTenant(out _).AddInvite(out var invite, i => i.Token = token.Value);

        // Assert
        invite.TokenHash.ShouldBe(InviteToken.HashOf(token.Value));
        invite.TokenHash.ShouldNotContain(token.Value);
        invite.Status.ShouldBe(InviteStatus.Pending());
        invite.DomainEvents.OfType<InviteCreated>().Single().InviteId.ShouldBe(invite.Id);
    }

    [Test, Property("Rule", "INV-4")]
    public void create_sets_the_expiry_seven_days_after_the_creation()
    {
        // Act
        var invite = PendingInvite();

        // Assert
        invite.ExpiresAt.ShouldBe(CreatedAt.AddDays(7));
    }

    [Test]
    public void create_keeps_no_email_when_the_request_gives_a_blank_one()
    {
        // Act
        var invite = PendingInvite(i => i.Email = "  ");

        // Assert
        invite.Email.ShouldBeNull();
    }

    [Test]
    public void create_refuses_an_email_that_is_not_an_email()
    {
        // Act
        var create = () => PendingInvite(i => i.Email = "not an email");

        // Assert
        create.ShouldThrow<FluentValidation.ValidationException>().Errors.ShouldAllBe(e => e.PropertyName == "email");
    }

    [Test, Property("Rule", "INV-9")]
    public void create_refuses_an_email_match_with_no_email()
    {
        // Act
        var create = () => PendingInvite(i =>
        {
            i.Email = "";
            i.RequireEmailMatch = true;
        });

        // Assert
        create.ShouldThrow<ValidationException>().Property.ShouldBe("email");
    }

    [Test, Property("Rule", "INV-7")]
    public void claim_adds_a_user_in_the_tenant_of_the_invite_with_its_role_and_the_sub_of_the_caller()
    {
        // Arrange
        var invite = PendingInvite(i => i.Role = UserRole.Admin().Value);

        // Act
        var user = invite.Claim(ClaimBy("sub-of-ada"), CreatedAt.AddDays(1));

        // Assert
        user.TenantId.ShouldBe(invite.TenantId);
        user.Role.ShouldBe(UserRole.Admin());
        user.Identifier.ShouldBe("sub-of-ada");
        user.UserPermissions.Select(p => p.Permission).ShouldBe(UserRole.Admin().GetDefaultPermissions(), ignoreOrder: true);
        invite.Status.ShouldBe(InviteStatus.Claimed());
        invite.ClaimedByUserId.ShouldBe(user.Id);
        invite.DomainEvents.OfType<InviteStatusChanged>().Single().Status.ShouldBe(InviteStatus.Claimed().Value);
    }

    [Test, Property("Rule", "INV-4")]
    public void claim_of_an_expired_invite_fails_with_gone()
    {
        // Arrange
        var invite = PendingInvite();

        // Act
        var claim = () => invite.Claim(ClaimBy("sub-of-ada"), CreatedAt + Invite.Lifetime);

        // Assert
        claim.ShouldThrow<GoneException>().Message.ShouldBe(Invite.ExpiredMessage);
        invite.Status.ShouldBe(InviteStatus.Pending());
    }

    [Test, Property("Rule", "INV-4")]
    public void claim_just_before_the_expiry_succeeds()
    {
        // Arrange
        var invite = PendingInvite();

        // Act
        var user = invite.Claim(ClaimBy("sub-of-ada"), CreatedAt + Invite.Lifetime - TimeSpan.FromTicks(1));

        // Assert
        user.ShouldNotBeNull();
    }

    [Test, Property("Rule", "INV-5")]
    public void second_claim_fails_with_gone()
    {
        // Arrange
        var invite = PendingInvite();
        invite.Claim(ClaimBy("sub-of-ada"), CreatedAt);

        // Act
        var claim = () => invite.Claim(ClaimBy("sub-of-bob"), CreatedAt);

        // Assert
        claim.ShouldThrow<GoneException>().Message.ShouldBe(Invite.UsedOrRevokedMessage);
    }

    [Test, Property("Rule", "INV-5")]
    public void claim_of_a_revoked_invite_fails_with_gone()
    {
        // Arrange
        var invite = PendingInvite();
        invite.Revoke();

        // Act
        var claim = () => invite.Claim(ClaimBy("sub-of-ada"), CreatedAt);

        // Assert
        claim.ShouldThrow<GoneException>().Message.ShouldBe(Invite.UsedOrRevokedMessage);
    }

    [Test, Property("Rule", "INV-9")]
    [Arguments("ada@example.com")]
    [Arguments("ADA@Example.com")]
    [Arguments(" ada@example.com ")]
    public void claim_with_an_email_match_takes_the_same_email_in_any_case(string tokenEmail)
    {
        // Arrange
        var invite = PendingInvite(i =>
        {
            i.Email = "Ada@example.com";
            i.RequireEmailMatch = true;
        });

        // Act
        var user = invite.Claim(ClaimBy("sub-of-ada", tokenEmail), CreatedAt);

        // Assert
        user.ShouldNotBeNull();
    }

    [Test, Property("Rule", "INV-9")]
    [Arguments("bob@example.com")]
    [Arguments(null)]
    public void claim_with_an_email_match_refuses_another_email(string? tokenEmail)
    {
        // Arrange
        var invite = PendingInvite(i =>
        {
            i.Email = "ada@example.com";
            i.RequireEmailMatch = true;
        });

        // Act
        var claim = () => invite.Claim(ClaimBy("sub-of-bob", tokenEmail), CreatedAt);

        // Assert
        claim.ShouldThrow<ForbiddenAccessException>().Message.ShouldBe(Invite.EmailMismatchMessage);
        invite.Status.ShouldBe(InviteStatus.Pending());
    }

    [Test, Property("Rule", "INV-9")]
    public void claim_with_no_email_match_takes_any_email()
    {
        // Arrange
        var invite = PendingInvite(i =>
        {
            i.Email = "ada@example.com";
            i.RequireEmailMatch = false;
        });

        // Act
        var user = invite.Claim(ClaimBy("sub-of-bob", "bob@example.com"), CreatedAt);

        // Assert
        user.ShouldNotBeNull();
    }

    [Test, Property("Rule", "INV-10")]
    public void revoke_moves_a_pending_invite_to_revoked()
    {
        // Arrange
        var invite = PendingInvite();

        // Act
        invite.Revoke();

        // Assert
        invite.Status.ShouldBe(InviteStatus.Revoked());
        invite.DomainEvents.OfType<InviteStatusChanged>().Single().Status.ShouldBe(InviteStatus.Revoked().Value);
    }

    [Test, Property("Rule", "INV-10")]
    public void revoke_of_a_revoked_invite_changes_nothing()
    {
        // Arrange
        var invite = PendingInvite();
        invite.Revoke();

        // Act
        invite.Revoke();

        // Assert
        invite.Status.ShouldBe(InviteStatus.Revoked());
        invite.DomainEvents.OfType<InviteStatusChanged>().Count().ShouldBe(1);
    }

    [Test, Property("Rule", "INV-10")]
    public void revoke_of_a_claimed_invite_fails()
    {
        // Arrange
        var invite = PendingInvite();
        invite.Claim(ClaimBy("sub-of-ada"), CreatedAt);

        // Act
        var revoke = () => invite.Revoke();

        // Assert
        revoke.ShouldThrow<ValidationException>().Message.ShouldBe("A claimed invite cannot be revoked.");
    }

    [Test, Property("Rule", "INV-10")]
    public void allowed_actions_give_revoke_only_for_a_pending_invite_and_a_caller_with_manage_users()
    {
        // Arrange
        var pending = PendingInvite();
        var revoked = PendingInvite();
        revoked.Revoke();
        var manager = new Caller.Member("usr_manager", [Permission.ManageUsers()]);
        var member = new Caller.Member("usr_member", [Permission.ReadUsers()]);

        // Act & Assert
        pending.AllowedActions(manager).ShouldBe([InviteAction.Revoke()]);
        pending.AllowedActions(member).ShouldBeEmpty();
        revoked.AllowedActions(manager).ShouldBeEmpty();
    }

    [Test, Property("Rule", "INV-10")]
    public void the_revoke_guard_refuses_a_caller_without_manage_users()
    {
        // Arrange
        var member = new Caller.Member("usr_member", [Permission.ReadUsers()]);

        // Act
        var guard = () => InviteAction.Revoke().GuardCaller(member);

        // Assert
        guard.ShouldThrow<ForbiddenAccessException>();
    }
}
