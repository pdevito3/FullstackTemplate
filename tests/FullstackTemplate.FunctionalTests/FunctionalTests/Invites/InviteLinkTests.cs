namespace FullstackTemplate.FunctionalTests.FunctionalTests.Invites;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Invites;
using FullstackTemplate.Server.Domain.Invites.Dtos;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Invite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using Shouldly;
using TestUtilities;

/// <summary>The invite link over HTTP: the admin creates it, a colleague with no user record previews and claims it.</summary>
public class InviteLinkTests : TestBase
{
    [Test, Property("Rule", "INV-7")]
    public async Task a_colleague_claims_the_link_of_an_admin_and_then_works_in_the_tenant()
    {
        // Arrange
        var admin = await AddAdminOfNewTenantAsync();
        FactoryClient.SignInAsAppUser(admin.Identifier);
        var created = await ReadAsync<AddInviteResponseDto>(await FactoryClient.PostJsonRequestAsync(ApiRoutes.Invites.Create,
            new FakeInviteForCreationDto().Generate() with { Role = UserRole.User().Value }), HttpStatusCode.Created);
        var colleague = $"sub-{Guid.NewGuid():N}";
        FactoryClient.SignInAsAppUser(colleague);
        var preview = await ReadAsync<InvitePreviewDto>(await FactoryClient.PostJsonRequestAsync(
            ApiRoutes.Invites.Preview, new InviteTokenDto { Token = created.Token }));
        var claimedBefore = ClaimedTotal(UserRole.User().Value);

        // Act
        var claim = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Invites.Claim, new FakeClaimInviteDto(created.Token).Generate());

        // Assert
        preview.Role.ShouldBe(UserRole.User().Value);
        var user = await ReadAsync<UserDto>(claim, HttpStatusCode.Created);
        claim.Headers.Location!.ToString().ShouldEndWith($"/users/{user.Id}");
        var me = await ReadAsync<UserDto>(await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetByIdentifier(colleague)));
        me.Id.ShouldBe(user.Id);
        me.Role.ShouldBe(UserRole.User().Value);
        var tenants = await ReadAsync<List<TenantDto>>(await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetList));
        tenants.ShouldHaveSingleItem().Id.ShouldBe(admin.TenantId);
        ClaimedTotal(UserRole.User().Value).ShouldBeGreaterThan(claimedBefore);
    }

    [Test, Property("Rule", "INV-5")]
    public async Task a_second_claim_of_a_link_gets_410_with_the_message_of_the_spec()
    {
        // Arrange
        var token = await AddInviteAsync();
        FactoryClient.SignInAsAppUser($"sub-{Guid.NewGuid():N}");
        (await FactoryClient.PostJsonRequestAsync(ApiRoutes.Invites.Claim, new FakeClaimInviteDto(token).Generate()))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        FactoryClient.SignInAsAppUser($"sub-{Guid.NewGuid():N}");

        // Act
        var claim = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Invites.Claim, new FakeClaimInviteDto(token).Generate());

        // Assert
        (await ProblemAsync(claim, HttpStatusCode.Gone)).Detail.ShouldBe(
            "This invite was used or revoked. Ask a tenant admin for a new invite.");
    }

    [Test, Property("Rule", "INV-6")]
    public async Task a_member_of_a_tenant_gets_409_with_the_message_of_the_spec()
    {
        // Arrange
        var token = await AddInviteAsync();
        var member = await AddAdminOfNewTenantAsync();
        FactoryClient.SignInAsAppUser(member.Identifier);

        // Act
        var claim = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Invites.Claim, new FakeClaimInviteDto(token).Generate());

        // Assert
        (await ProblemAsync(claim, HttpStatusCode.Conflict)).Detail.ShouldBe("Your account already belongs to a tenant.");
    }

    [Test]
    public async Task an_unknown_link_gets_404_with_the_message_of_the_spec()
    {
        // Arrange
        FactoryClient.SignInAsAppUser($"sub-{Guid.NewGuid():N}");

        // Act
        var preview = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Invites.Preview,
            new InviteTokenDto { Token = InviteToken.New().Value });

        // Assert
        (await ProblemAsync(preview, HttpStatusCode.NotFound)).Detail.ShouldBe(
            "This invite link is not valid. Ask a tenant admin for a new invite.");
    }

    [Test, Property("Rule", "INV-4")]
    public async Task an_expired_link_gets_410_with_the_message_of_the_spec()
    {
        // Arrange
        var token = await AddInviteAsync(DateTimeOffset.UtcNow - Invite.Lifetime - TimeSpan.FromMinutes(1));
        FactoryClient.SignInAsAppUser($"sub-{Guid.NewGuid():N}");

        // Act
        var preview = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Invites.Preview, new InviteTokenDto { Token = token });

        // Assert
        (await ProblemAsync(preview, HttpStatusCode.Gone)).Detail.ShouldBe(
            "This invite expired. Ask a tenant admin for a new invite.");
    }

    [Test, Property("Rule", "INV-9")]
    public async Task a_claim_with_another_email_in_the_token_gets_403_with_the_message_of_the_spec()
    {
        // Arrange
        var token = InviteToken.New().Value;
        TestGraph.Start(out _).AddTenant(out var tenant).AddInvite(out var invite, i =>
        {
            i.Token = token;
            i.Email = "ada@example.com";
            i.RequireEmailMatch = true;
        });
        await InsertAsync(tenant);
        await InsertAsync(invite);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["sub"] = $"sub-{Guid.NewGuid():N}",
            ["azp"] = TestingWebApplicationFactory.FirstPartyClientId,
            ["email"] = "bob@example.com",
        });

        // Act
        var claim = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Invites.Claim, new FakeClaimInviteDto(token).Generate());

        // Assert
        (await ProblemAsync(claim, HttpStatusCode.Forbidden)).Detail.ShouldBe(
            "This invite is for another email address. Sign in with the account of that email.");
    }

    [Test, Property("Rule", "INV-9")]
    public async Task a_claim_with_the_verified_email_of_the_invite_in_the_token_adds_the_user()
    {
        // Arrange
        var token = await AddEmailMatchInviteAsync("ada@example.com");
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["sub"] = $"sub-{Guid.NewGuid():N}",
            ["azp"] = TestingWebApplicationFactory.FirstPartyClientId,
            ["email"] = "Ada@example.com",
            // The fake bearer handler takes no boolean. CurrentUserServiceTests reads the boolean of a real JWT.
            ["email_verified"] = "true",
        });

        // Act
        var claim = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Invites.Claim, new FakeClaimInviteDto(token).Generate());

        // Assert
        claim.StatusCode.ShouldBe(HttpStatusCode.Created, await claim.Content.ReadAsStringAsync());
    }

    [Test, Property("Rule", "INV-9")]
    public async Task a_claim_with_the_email_of_the_invite_and_no_email_verified_in_the_token_gets_403()
    {
        // Arrange
        var token = await AddEmailMatchInviteAsync("ada@example.com");
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["sub"] = $"sub-{Guid.NewGuid():N}",
            ["azp"] = TestingWebApplicationFactory.FirstPartyClientId,
            ["email"] = "ada@example.com",
        });

        // Act
        var claim = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Invites.Claim, new FakeClaimInviteDto(token).Generate());

        // Assert
        (await ProblemAsync(claim, HttpStatusCode.Forbidden)).Detail.ShouldBe(
            "The email address of your account is not verified. Verify it, then open the invite link again.");
    }

    private static async Task<string> AddEmailMatchInviteAsync(string email)
    {
        var token = InviteToken.New().Value;
        TestGraph.Start(out _).AddTenant(out var tenant).AddInvite(out var invite, i =>
        {
            i.Token = token;
            i.Email = email;
            i.RequireEmailMatch = true;
        });
        await InsertAsync(tenant);
        await InsertAsync(invite);
        return token;
    }

    private static async Task<User> AddAdminOfNewTenantAsync()
    {
        TestGraph.Start(out _).AddTenant(out var tenant).AddUser(out var admin, u => u.Role = UserRole.Admin().Value);
        await InsertAsync(tenant);
        await InsertAsync(admin);
        return admin;
    }

    private static async Task<string> AddInviteAsync(DateTimeOffset? createdAt = null)
    {
        var token = InviteToken.New().Value;
        TestGraph.Start(out _).AddTenant(out Tenant tenant).AddInvite(out var invite, i =>
        {
            i.Token = token;
            i.CreatedAt = createdAt;
        });
        await InsertAsync(tenant);
        await InsertAsync(invite);
        return token;
    }

    private static long ClaimedTotal(string role)
    {
        var factory = TestingWebApplicationFactory.Instance;
        factory.Services.GetRequiredService<MeterProvider>().ForceFlush().ShouldBeTrue();
        return factory.ExportedMetrics.Snapshot()
            .Where(m => m.Name == "app.invites.claimed" && m.MeterName == "FullstackTemplate.Server")
            .SelectMany(m => m.MetricPoints)
            .Where(p => HasRole(p, role))
            .Select(p => p.GetSumLong())
            .DefaultIfEmpty(0)
            .Last();
    }

    private static bool HasRole(MetricPoint point, string role)
    {
        foreach (var tag in point.Tags)
            if (tag.Key == "app.user.role" && Equals(tag.Value, role))
                return true;
        return false;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
        where T : class
    {
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>()).ShouldNotBeNull();
    }

    private static Task<ProblemDetails> ProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
        => ReadAsync<ProblemDetails>(response, expected);
}
