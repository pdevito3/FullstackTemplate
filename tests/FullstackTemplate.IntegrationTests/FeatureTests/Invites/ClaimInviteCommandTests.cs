namespace FullstackTemplate.IntegrationTests.FeatureTests.Invites;

using FullstackTemplate.IntegrationTests.Databases;
using FullstackTemplate.Server.Domain.Invites;
using FullstackTemplate.Server.Domain.Invites.Features;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Invite;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class ClaimInviteCommandTests : TestBase
{
    private static ClaimInvite.Command ClaimOf(string token) => new(new FakeClaimInviteDto(token).Generate());

    [Test, Property("Rule", "INV-7")]
    public async Task a_claim_adds_a_user_in_the_tenant_of_the_invite_with_its_role_and_the_sub_of_the_caller()
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
            })
            .BuildAsync();
        var subject = scope.SignInAsNewUser();
        var command = ClaimOf(token);

        // Act
        var user = await scope.SendAsync(command);

        // Assert
        user.Identifier.ShouldBe(subject);
        user.Role.ShouldBe(UserRole.Admin().Value);
        user.FirstName.ShouldBe(command.Dto.FirstName);
        user.Email.ShouldBe(command.Dto.Email);
        user.Permissions.ShouldBe(UserRole.Admin().GetDefaultPermissions().Select(p => p.Value), ignoreOrder: true);
        var stored = await scope.ExecuteDbContextAsync(db => db.Invites.IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleAsync(i => i.Id == invite.Id));
        stored.Status.ShouldBe(InviteStatus.Claimed());
        stored.ClaimedByUserId.ShouldBe(user.Id);
        (await scope.ExecuteDbContextAsync(db => db.Users.IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleAsync(u => u.Id == user.Id))).TenantId.ShouldBe(tenant.Id);
    }

    [Test, Property("Rule", "INV-7")]
    public async Task after_a_claim_the_next_request_of_the_caller_is_in_the_new_tenant()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _).AddTenant(out var tenant).AddInvite(out _, i =>
        {
            i.Token = token;
            i.Role = UserRole.Admin().Value;
        }).BuildAsync();
        var subject = scope.SignInAsNewUser();
        // The lookup of the tenant of the caller misses before the claim, and a miss is cached
        var before = new TestingServiceScope();
        before.SetUserWithDefaults(identifier: subject);
        (await before.SendAsync(new GetTenantList.Query(TenantParametersDto.From(null, null, null, null)))).ShouldBeEmpty();

        // Act
        await scope.SendAsync(ClaimOf(token));

        // Assert
        var after = new TestingServiceScope();
        after.SetUserWithDefaults(identifier: subject);
        (await after.SendAsync(new GetTenantList.Query(TenantParametersDto.From(null, null, null, null)))).Select(t => t.Id).ShouldBe([tenant.Id]);
    }

    [Test, Property("Rule", "INV-6")]
    public async Task a_caller_with_a_live_user_in_any_tenant_cannot_claim()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _).AddTenant(out _).AddInvite(out var invite, i => i.Token = token).BuildAsync();

        // Act
        var act = () => scope.SendAsync(ClaimOf(token));

        // Assert
        (await act.ShouldThrowAsync<ConflictException>()).Message.ShouldBe(Invite.CallerHasTenantMessage);
        (await InviteAsync(invite.Id)).Status.ShouldBe(InviteStatus.Pending());
    }

    [Test, Property("Rule", "INV-6")]
    public async Task a_machine_client_cannot_claim()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _).AddTenant(out _).AddInvite(out _, i => i.Token = token).BuildAsync();
        scope.SetMachineClient("reporting-job", serviceAccountSubject: $"service-account-{Guid.NewGuid():N}");

        // Act
        var act = () => scope.SendAsync(ClaimOf(token));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task an_unknown_token_gives_not_found()
    {
        // Arrange
        var scope = new TestingServiceScope();
        scope.SignInAsNewUser();

        // Act
        var act = () => scope.SendAsync(ClaimOf(InviteToken.New().Value));

        // Assert
        (await act.ShouldThrowAsync<NotFoundException>()).Message.ShouldBe(Invite.NotValidMessage);
    }

    [Test, Property("Rule", "INV-4")]
    public async Task a_claim_of_an_expired_invite_fails_with_gone_and_adds_no_user()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _).AddTenant(out var tenant).AddInvite(out _, i =>
        {
            i.Token = token;
            i.CreatedAt = DateTimeOffset.UtcNow - Invite.Lifetime - TimeSpan.FromMinutes(1);
        }).BuildAsync();
        scope.SignInAsNewUser();

        // Act
        var act = () => scope.SendAsync(ClaimOf(token));

        // Assert
        (await act.ShouldThrowAsync<GoneException>()).Message.ShouldBe(Invite.ExpiredMessage);
        (await LiveUserCountAsync(tenant.Id)).ShouldBe(0);
    }

    [Test, Property("Rule", "INV-5")]
    public async Task a_second_claim_fails_with_gone()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _).AddTenant(out var tenant).AddInvite(out _, i => i.Token = token).BuildAsync();
        scope.SignInAsNewUser();
        await scope.SendAsync(ClaimOf(token));
        var second = new TestingServiceScope();
        second.SignInAsNewUser();

        // Act
        var act = () => second.SendAsync(ClaimOf(token));

        // Assert
        (await act.ShouldThrowAsync<GoneException>()).Message.ShouldBe(Invite.UsedOrRevokedMessage);
        (await LiveUserCountAsync(tenant.Id)).ShouldBe(1);
    }

    [Test, Property("Rule", "INV-5")]
    public async Task a_claim_that_a_rival_claim_beat_after_the_load_fails_with_gone()
    {
        // Arrange
        var pause = PauseAtQuery.After("invites.claim.load");
        await using var services = pause.BuildServices();
        var token = InviteToken.New().Value;
        await new TestingServiceScope().StartGraph(out _).AddTenant(out var tenant).AddInvite(out _, i => i.Token = token).BuildAsync();
        var underTest = new TestingServiceScope(services);
        underTest.SignInAsNewUser();
        var rival = new TestingServiceScope();
        var rivalSubject = rival.SignInAsNewUser();

        // Act
        var claim = underTest.SendAsync(ClaimOf(token));
        await pause.ReachedAsync();
        await rival.SendAsync(ClaimOf(token));
        pause.Release();

        // Assert
        (await Should.ThrowAsync<GoneException>(claim)).Message.ShouldBe(Invite.UsedOrRevokedMessage);
        var users = await LiveUsersAsync(tenant.Id);
        users.ShouldHaveSingleItem().Identifier.ShouldBe(rivalSubject);
    }

    [Test, Property("Rule", "INV-6")]
    public async Task two_claims_of_one_sub_at_the_same_time_add_one_user_and_the_second_gets_a_conflict()
    {
        // Arrange
        var pause = PauseAtQuery.After("invites.claim.load");
        await using var services = pause.BuildServices();
        var (first, second) = (InviteToken.New().Value, InviteToken.New().Value);
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var firstTenant).AddInvite(out _, i => i.Token = first)
            .AddTenant(out var secondTenant).AddInvite(out _, i => i.Token = second)
            .BuildAsync();
        var underTest = new TestingServiceScope(services);
        var subject = underTest.SignInAsNewUser();
        var rival = new TestingServiceScope();
        rival.SetUserWithDefaults(identifier: subject);

        // Act
        var claim = underTest.SendAsync(ClaimOf(first));
        await pause.ReachedAsync();
        await rival.SendAsync(ClaimOf(second));
        pause.Release();

        // Assert
        (await Should.ThrowAsync<ConflictException>(claim)).Message.ShouldBe(Invite.CallerHasTenantMessage);
        (await LiveUserCountAsync(firstTenant.Id)).ShouldBe(0);
        (await LiveUserCountAsync(secondTenant.Id)).ShouldBe(1);
    }

    [Test, Property("Rule", "INV-7")]
    public async Task a_tenant_delete_during_a_claim_waits_and_then_deletes_the_new_user()
    {
        // Arrange
        var pause = PauseAtQuery.After("invites.claim.load");
        await using var services = pause.BuildServices();
        var token = InviteToken.New().Value;
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddInvite(out _, i => i.Token = token)
            .BuildAsync();
        var underTest = new TestingServiceScope(services);
        underTest.SignInAsNewUser();
        var rival = new TestingServiceScope();
        rival.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var claim = underTest.SendAsync(ClaimOf(token));
        await pause.ReachedAsync();
        var delete = rival.SendAsync(new DeleteTenant.Command(tenant.Id));
        await pause.WaitUntilBlockedAsync(delete);
        pause.Release();

        // Assert
        var user = await claim;
        await delete;
        (await new TestingServiceScope().ExecuteDbContextAsync(db => db.Users
            .IgnoreQueryFilters([QueryFilterNames.Tenant, QueryFilterNames.SoftDelete])
            .SingleAsync(u => u.Id == user.Id))).IsDeleted.ShouldBeTrue();
        (await LiveUserCountAsync(tenant.Id)).ShouldBe(0);
    }

    [Test, Property("Rule", "INV-11")]
    public async Task a_claim_that_a_tenant_delete_beat_after_the_token_lookup_finds_no_invite()
    {
        // Arrange
        var pause = PauseAtQuery.After("invites.claim.find_tenant");
        await using var services = pause.BuildServices();
        var token = InviteToken.New().Value;
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddInvite(out var invite, i => i.Token = token)
            .BuildAsync();
        var underTest = new TestingServiceScope(services);
        underTest.SignInAsNewUser();
        var rival = new TestingServiceScope();
        rival.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var claim = underTest.SendAsync(ClaimOf(token));
        await pause.ReachedAsync();
        await rival.SendAsync(new DeleteTenant.Command(tenant.Id));
        pause.Release();

        // Assert
        (await Should.ThrowAsync<NotFoundException>(claim)).Message.ShouldBe(Invite.NotValidMessage);
        (await new TestingServiceScope().ExecuteDbContextAsync(db => db.Invites
            .IgnoreQueryFilters([QueryFilterNames.Tenant, QueryFilterNames.SoftDelete])
            .SingleAsync(i => i.Id == invite.Id))).IsDeleted.ShouldBeTrue();
        (await LiveUserCountAsync(tenant.Id)).ShouldBe(0);
    }

    [Test, Property("Rule", "INV-9")]
    public async Task a_claim_with_an_email_match_takes_the_email_of_the_token_in_any_case_and_gives_it_to_the_user()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _).AddTenant(out _).AddInvite(out _, i =>
        {
            i.Token = token;
            i.Email = "ada@example.com";
            i.RequireEmailMatch = true;
        }).BuildAsync();
        scope.SignInAsNewUser(email: "ADA@example.com");
        var command = new ClaimInvite.Command(new FakeClaimInviteDto(token).Generate() with { Email = "someone.else@example.com" });

        // Act
        var user = await scope.SendAsync(command);

        // Assert
        user.Email.ShouldBe("ADA@example.com");
        (await LiveUsersAsync(user.TenantId)).ShouldHaveSingleItem().Email.Value.ShouldBe("ADA@example.com");
    }

    [Test, Property("Rule", "INV-9")]
    public async Task a_claim_with_an_email_match_refuses_another_email_of_the_token()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _).AddTenant(out var tenant).AddInvite(out var invite, i =>
        {
            i.Token = token;
            i.Email = "ada@example.com";
            i.RequireEmailMatch = true;
        }).BuildAsync();
        scope.SignInAsNewUser(email: "bob@example.com");
        // The body cannot pass the check, because the email of the token decides
        var command = new ClaimInvite.Command(new FakeClaimInviteDto(token).Generate() with { Email = "ada@example.com" });

        // Act
        var act = () => scope.SendAsync(command);

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe(Invite.EmailMismatchMessage);
        (await InviteAsync(invite.Id)).Status.ShouldBe(InviteStatus.Pending());
        (await LiveUserCountAsync(tenant.Id)).ShouldBe(0);
    }

    [Test, Property("Rule", "INV-9")]
    public async Task a_claim_with_an_email_match_refuses_the_email_of_the_token_when_the_provider_did_not_verify_it()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var token = InviteToken.New().Value;
        await scope.StartGraph(out _).AddTenant(out var tenant).AddInvite(out var invite, i =>
        {
            i.Token = token;
            i.Email = "ada@example.com";
            i.RequireEmailMatch = true;
        }).BuildAsync();
        // A self-registered account with the email of the invitee, as a holder of a forwarded link makes it
        scope.SignInAsNewUser(email: "ada@example.com", emailVerified: false);

        // Act
        var act = () => scope.SendAsync(ClaimOf(token));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe(Invite.EmailNotVerifiedMessage);
        (await InviteAsync(invite.Id)).Status.ShouldBe(InviteStatus.Pending());
        (await LiveUserCountAsync(tenant.Id)).ShouldBe(0);
    }

    private static Task<Invite> InviteAsync(string id)
        => new TestingServiceScope().ExecuteDbContextAsync(db => db.Invites
            .IgnoreQueryFilters([QueryFilterNames.Tenant]).SingleAsync(i => i.Id == id));

    private static Task<List<User>> LiveUsersAsync(string tenantId)
        => new TestingServiceScope().ExecuteDbContextAsync(db => db.Users
            .IgnoreQueryFilters([QueryFilterNames.Tenant]).Where(u => u.TenantId == tenantId).ToListAsync());

    private static async Task<int> LiveUserCountAsync(string tenantId) => (await LiveUsersAsync(tenantId)).Count;
}
