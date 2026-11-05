namespace FullstackTemplate.IntegrationTests.FeatureTests.Invites;

using FullstackTemplate.Server.Resources;
using FullstackTemplate.Server.Domain.Invites;
using FullstackTemplate.Server.Domain.Invites.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Invite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class RevokeInviteCommandTests : TestBase
{
    [Test, Property("Rule", "INV-10")]
    public async Task a_user_with_manage_users_revokes_a_pending_invite_of_the_tenant()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var graph = scope.StartGraph(out var seed).AddInvite(out var invite)
            .Under(seed.Tenant).AddUser(out var manager, u => u.Role = UserRole.User().Value);
        manager.AddPermissions([Permission.ManageUsers()]);
        await graph.BuildAsync();
        scope.SetUserWithDefaults(identifier: manager.Identifier);

        // Act
        var revoked = await scope.SendAsync(new RevokeInvite.Command(invite.Id));

        // Assert
        revoked.Status.ShouldBe(InviteStatus.Revoked().Value);
        revoked.AllowedActions.ShouldBeEmpty();
        (await scope.ExecuteDbContextAsync(db => db.Invites.SingleAsync(i => i.Id == invite.Id)))
            .Status.ShouldBe(InviteStatus.Revoked());
    }

    [Test, Property("Rule", "INV-10")]
    public async Task a_revoke_of_a_revoked_invite_succeeds_and_changes_nothing()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _).AddInvite(out var invite).BuildAsync();
        var first = await scope.SendAsync(new RevokeInvite.Command(invite.Id));

        // Act
        var second = await new TestingServiceScope().SendAsync(new RevokeInvite.Command(invite.Id));

        // Assert
        second.Status.ShouldBe(InviteStatus.Revoked().Value);
        second.Version.ShouldBe(first.Version);
    }

    [Test, Property("Rule", "INV-10")]
    public async Task a_member_without_manage_users_cannot_revoke()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out var seed).AddInvite(out var invite)
            .Under(seed.Tenant).AddUser(out var member, u => u.Role = UserRole.User().Value)
            .BuildAsync();
        scope.SetUserWithDefaults(identifier: member.Identifier);

        // Act
        var act = () => scope.SendAsync(new RevokeInvite.Command(invite.Id));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test, Property("Rule", "INV-10")]
    public async Task an_admin_cannot_revoke_an_invite_of_another_tenant()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _).AddTenant(out _).AddInvite(out var foreign).BuildAsync();

        // Act
        var act = () => scope.SendAsync(new RevokeInvite.Command(foreign.Id));

        // Assert
        await act.ShouldThrowAsync<NotFoundException>();
        (await scope.ExecuteDbContextAsync(db => db.Invites.IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleAsync(i => i.Id == foreign.Id))).Status.ShouldBe(InviteStatus.Pending());
    }

    [Test, Property("Rule", "INV-10")]
    public async Task a_claimed_invite_cannot_be_revoked()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _).AddInvite(out var invite, i => i.Token = token).BuildAsync();
        var claimer = new TestingServiceScope();
        claimer.SignInAsNewUser();
        await claimer.SendAsync(new ClaimInvite.Command(new FakeClaimInviteDto(token).Generate()));

        // Act
        var act = () => scope.SendAsync(new RevokeInvite.Command(invite.Id));

        // Assert
        (await act.ShouldThrowAsync<ValidationException>()).Message.ShouldBe("A claimed invite cannot be revoked.");
    }
}
