namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class TenantIsolationTests : TestBase
{
    [Test]
    public async Task user_sees_only_the_users_in_their_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync(canReadUsers: true);
        var (_, otherUser) = await AddTenantWithUserAsync();
        FactoryClient.SignInAsAppUser(user.Identifier);

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?pageSize=500");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var users = (await result.Content.ReadFromJsonAsync<List<UserDto>>()).ShouldNotBeNull();
        users.ShouldContain(u => u.Id == user.Id);
        users.ShouldNotContain(u => u.Id == otherUser.Id);
    }

    [Test]
    public async Task signed_in_user_with_no_user_record_sees_no_tenant_data()
    {
        // Arrange
        await AddTenantWithUserAsync();
        FactoryClient.SignInAsAppUser($"unknown-{Guid.NewGuid()}");

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?pageSize=500");

        // Assert
        await ShouldBeForbiddenAsync(result, "You need a user record in a tenant to do this.");
    }

    [Test]
    public async Task user_sees_their_tenant_as_soon_as_their_user_record_is_added()
    {
        // Arrange
        var identifier = $"new-{Guid.NewGuid()}";
        FactoryClient.SignInAsAppUser(identifier);
        var before = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?pageSize=500");
        before.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var (_, user) = await AddTenantWithUserAsync(identifier, canReadUsers: true);

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?pageSize=500");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var users = (await result.Content.ReadFromJsonAsync<List<UserDto>>()).ShouldNotBeNull();
        users.ShouldHaveSingleItem().Id.ShouldBe(user.Id);
    }

    [Test]
    public async Task machine_client_not_on_the_allow_list_sees_no_tenant_data()
    {
        // Arrange
        await AddTenantWithUserAsync();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["client_id"] = "m2m" });

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?pageSize=500");

        // Assert
        await ShouldBeForbiddenAsync(result,
            "This client is not a registered agent of your tenant. A tenant admin can register it.");
    }

    [Test]
    public async Task machine_client_on_the_allow_list_sees_all_tenants()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        var (_, otherUser) = await AddTenantWithUserAsync();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

        // Act
        var result = await FactoryClient.GetRequestAsync(
            $"{ApiRoutes.Users.GetList}?pageSize=500&filters=id == \"{user.Id}\" || id == \"{otherUser.Id}\"");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var users = (await result.Content.ReadFromJsonAsync<List<UserDto>>()).ShouldNotBeNull();
        users.Select(u => u.Id).ShouldBe([user.Id, otherUser.Id], ignoreOrder: true);
    }

    private static async Task ShouldBeForbiddenAsync(HttpResponseMessage result, string detail)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problem.Detail.ShouldBe(detail);
    }

    private static async Task<(Tenant Tenant, User User)> AddTenantWithUserAsync(
        string? identifier = null, bool canReadUsers = false)
    {
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        var userForCreation = new FakeUserForCreation().RuleFor(u => u.TenantId, tenant.Id);
        if (identifier is not null)
            userForCreation.RuleFor(u => u.Identifier, identifier);
        var user = User.Create(userForCreation.Generate());
        if (canReadUsers)
            user.AddPermission(Permission.ReadUsers());
        await InsertAsync(tenant);
        await InsertAsync(user);
        return (tenant, user);
    }
}
