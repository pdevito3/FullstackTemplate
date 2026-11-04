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

public class AddInviteCommandTests : TestBase
{
    [Test, Property("Rule", "INV-2")]
    public async Task add_gives_the_token_once_and_stores_only_its_hash()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var dto = new FakeInviteForCreationDto().Generate() with { Role = UserRole.User().Value };

        // Act
        var added = await scope.SendAsync(new AddInvite.Command(dto));

        // Assert
        added.Token.Length.ShouldBe(InviteToken.Length);
        added.Invite.Status.ShouldBe(InviteStatus.Pending().Value);
        added.Invite.TenantId.ShouldBe(TestFixture.DefaultTenant.Id);
        added.Invite.AllowedActions.ShouldBe([InviteAction.Revoke().Value]);
        var stored = await scope.ExecuteDbContextAsync(db => db.Invites.AsNoTracking().SingleAsync(i => i.Id == added.Invite.Id));
        stored.TenantId.ShouldBe(TestFixture.DefaultTenant.Id);
        stored.TokenHash.ShouldBe(InviteToken.HashOf(added.Token));
        var rowText = await scope.ExecuteDbContextAsync(db => db.Database
            .SqlQuery<string>($"SELECT row_to_json(i)::text AS \"Value\" FROM invites i WHERE i.id = {added.Invite.Id}")
            .SingleAsync());
        rowText.ShouldNotContain(added.Token);
    }

    [Test, Property("Rule", "INV-4")]
    public async Task add_sets_the_expiry_seven_days_after_now()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var before = DateTimeOffset.UtcNow;

        // Act
        var added = await scope.SendAsync(new AddInvite.Command(
            new FakeInviteForCreationDto().Generate() with { Role = UserRole.User().Value }));

        // Assert
        added.Invite.ExpiresAt.ShouldBeInRange(before + Invite.Lifetime, DateTimeOffset.UtcNow + Invite.Lifetime);
    }

    [Test, Property("Rule", "INV-1")]
    public async Task a_member_without_manage_users_cannot_add_an_invite()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _).AddUser(out var member, u => u.Role = UserRole.User().Value).BuildAsync();
        scope.SetUserWithDefaults(identifier: member.Identifier);

        // Act
        var act = () => scope.SendAsync(new AddInvite.Command(
            new FakeInviteForCreationDto().Generate() with { Role = UserRole.User().Value }));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test, Property("Rule", "INV-1")]
    public async Task a_caller_cannot_invite_with_a_role_that_has_more_permissions_than_the_caller()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var graph = scope.StartGraph(out _).AddUser(out var manager, u => u.Role = UserRole.User().Value);
        manager.AddPermissions([Permission.ManageUsers()]);
        await graph.BuildAsync();
        scope.SetUserWithDefaults(identifier: manager.Identifier);

        // Act
        var asAdmin = () => scope.SendAsync(new AddInvite.Command(
            new FakeInviteForCreationDto().Generate() with { Role = UserRole.Admin().Value }));
        var asUser = await scope.SendAsync(new AddInvite.Command(
            new FakeInviteForCreationDto().Generate() with { Role = UserRole.User().Value }));

        // Assert
        await asAdmin.ShouldThrowAsync<ForbiddenAccessException>();
        asUser.Invite.Role.ShouldBe(UserRole.User().Value);
    }

    [Test]
    public async Task a_user_cannot_invite_to_another_tenant()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _).AddTenant(out var other).BuildAsync();

        // Act
        var act = () => scope.SendAsync(new AddInvite.Command(
            new FakeInviteForCreationDto().Generate() with { Role = UserRole.User().Value, TenantId = other.Id }));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
        (await scope.ExecuteDbContextAsync(db => db.Invites.IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AnyAsync(i => i.TenantId == other.Id))).ShouldBeFalse();
    }

    [Test, Property("Rule", "INV-9")]
    public async Task an_email_match_with_no_email_is_refused()
    {
        // Arrange
        var scope = new TestingServiceScope();

        // Act
        var act = () => scope.SendAsync(new AddInvite.Command(new FakeInviteForCreationDto().Generate() with
        {
            Role = UserRole.User().Value,
            Email = null,
            RequireEmailMatch = true,
        }));

        // Assert
        (await act.ShouldThrowAsync<ValidationException>()).Property.ShouldBe("email");
    }
}
