namespace FullstackTemplate.IntegrationTests.FeatureTests.Invites;

using FullstackTemplate.Server.Domain.Invites;
using FullstackTemplate.Server.Domain.Invites.Dtos;
using FullstackTemplate.Server.Domain.Invites.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;

public class GetInvitePreviewQueryTests : TestBase
{
    private static GetInvitePreview.Query PreviewOf(string token) => new(new InviteTokenDto { Token = token });

    [Test]
    public async Task a_new_user_previews_the_tenant_the_role_the_expiry_and_the_email_match()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddInvite(out var invite, i =>
            {
                i.Token = token;
                i.Role = UserRole.Admin().Value;
                i.Email = "ada@example.com";
                i.RequireEmailMatch = true;
            })
            .BuildAsync();
        scope.SignInAsNewUser();

        // Act
        var preview = await scope.SendAsync(PreviewOf(token));

        // Assert
        preview.ShouldBe(new InvitePreviewDto
        {
            TenantName = tenant.Name,
            Role = UserRole.Admin().Value,
            ExpiresAt = invite.ExpiresAt,
            RequireEmailMatch = true,
        });
    }

    [Test]
    public async Task a_member_of_another_tenant_can_preview()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _).AddTenant(out var tenant).AddInvite(out _, i => i.Token = token).BuildAsync();

        // Act
        var preview = await scope.SendAsync(PreviewOf(token));

        // Assert
        preview.TenantName.ShouldBe(tenant.Name);
    }

    [Test]
    public async Task an_unknown_token_gives_not_found()
    {
        // Arrange
        var scope = new TestingServiceScope();
        scope.SignInAsNewUser();

        // Act
        var act = () => scope.SendAsync(PreviewOf(InviteToken.New().Value));

        // Assert
        (await act.ShouldThrowAsync<NotFoundException>()).Message.ShouldBe(Invite.NotValidMessage);
    }

    [Test, Property("Rule", "INV-2")]
    public async Task the_hash_of_a_token_is_not_a_token()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _).AddTenant(out _).AddInvite(out var invite, i => i.Token = token).BuildAsync();
        scope.SignInAsNewUser();

        // Act
        var act = () => scope.SendAsync(PreviewOf(invite.TokenHash));

        // Assert
        await act.ShouldThrowAsync<NotFoundException>();
    }

    [Test, Property("Rule", "INV-4")]
    public async Task an_expired_invite_gives_gone()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _).AddTenant(out _)
            .AddInvite(out _, i =>
            {
                i.Token = token;
                i.CreatedAt = DateTimeOffset.UtcNow - Invite.Lifetime - TimeSpan.FromMinutes(1);
            })
            .BuildAsync();
        scope.SignInAsNewUser();

        // Act
        var act = () => scope.SendAsync(PreviewOf(token));

        // Assert
        (await act.ShouldThrowAsync<GoneException>()).Message.ShouldBe(Invite.ExpiredMessage);
    }

    [Test, Property("Rule", "INV-5")]
    public async Task a_revoked_invite_gives_gone()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        var graph = scope.StartGraph(out _).AddTenant(out _).AddInvite(out var invite, i => i.Token = token);
        invite.Revoke();
        await graph.BuildAsync();
        scope.SignInAsNewUser();

        // Act
        var act = () => scope.SendAsync(PreviewOf(token));

        // Assert
        (await act.ShouldThrowAsync<GoneException>()).Message.ShouldBe(Invite.UsedOrRevokedMessage);
    }

    [Test, Property("Rule", "INV-6")]
    public async Task a_machine_client_cannot_preview()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _).AddTenant(out _).AddInvite(out _, i => i.Token = token).BuildAsync();
        scope.SetMachineClient("reporting-job");

        // Act
        var act = () => scope.SendAsync(PreviewOf(token));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }
}
