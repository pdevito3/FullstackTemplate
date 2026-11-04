namespace FullstackTemplate.IntegrationTests.FeatureTests.Invites;

using FullstackTemplate.Server.Domain.Invites;
using FullstackTemplate.Server.Domain.Invites.Dtos;
using FullstackTemplate.Server.Domain.Invites.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;

public class GetInviteListQueryTests : TestBase
{
    private static GetInviteList.Query AllInvites() => new(InviteParametersDto.From(1, 100, null, null));

    [Test, Property("Rule", "INV-2")]
    public async Task the_list_gives_the_invites_of_the_own_tenant_with_no_token()
    {
        // Arrange
        var scope = new TestingServiceScope();
        // A tenant of its own, so the list shows only the invites of the test
        await scope.StartGraph(out _)
            .AddTenant(out _).AddInvite(out var foreign)
            .AddTenant(out var tenant).AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddInvite(out var own, i => i.Email = "ada@example.com")
            .BuildAsync();
        scope.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var invites = await scope.SendAsync(AllInvites());

        // Assert
        var listed = invites.ShouldHaveSingleItem();
        listed.Id.ShouldBe(own.Id);
        listed.TenantId.ShouldBe(tenant.Id);
        listed.Email.ShouldBe("ada@example.com");
        listed.AllowedActions.ShouldBe([InviteAction.Revoke().Value]);
        typeof(InviteDto).GetProperties().Select(p => p.Name).ShouldNotContain(n => n.Contains("Token"));
        invites.ShouldNotContain(i => i.Id == foreign.Id);
    }

    [Test]
    public async Task the_list_gives_no_action_for_an_invite_that_is_not_pending()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var graph = scope.StartGraph(out _)
            .AddTenant(out var tenant).AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddInvite(out var revoked, i => i.Email = "");
        revoked.Revoke();
        await graph.BuildAsync();
        scope.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var invites = await scope.SendAsync(AllInvites());

        // Assert
        var listed = invites.ShouldHaveSingleItem();
        listed.Status.ShouldBe(InviteStatus.Revoked().Value);
        listed.Email.ShouldBeNull();
        listed.AllowedActions.ShouldBeEmpty();
    }

    [Test]
    public async Task a_member_without_manage_users_cannot_list_invites()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _).AddUser(out var member, u => u.Role = UserRole.User().Value).BuildAsync();
        scope.SetUserWithDefaults(identifier: member.Identifier);

        // Act
        var act = () => scope.SendAsync(AllInvites());

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }
}
