namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Shouldly;
using TestUtilities;

public class TenantIsolationTests : TestBase
{
    [Test]
    public async Task user_sees_only_the_users_in_their_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        var (_, otherUser) = await AddTenantWithUserAsync();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });

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
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = $"unknown-{Guid.NewGuid()}" });

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?pageSize=500");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var users = (await result.Content.ReadFromJsonAsync<List<UserDto>>()).ShouldNotBeNull();
        users.ShouldBeEmpty();
    }

    [Test]
    public async Task user_sees_their_tenant_as_soon_as_their_user_record_is_added()
    {
        // Arrange
        var identifier = $"new-{Guid.NewGuid()}";
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = identifier });
        var before = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?pageSize=500");
        before.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (_, user) = await AddTenantWithUserAsync(identifier);

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
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var users = (await result.Content.ReadFromJsonAsync<List<UserDto>>()).ShouldNotBeNull();
        users.ShouldBeEmpty();
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

    private static async Task<(Tenant Tenant, User User)> AddTenantWithUserAsync(string? identifier = null)
    {
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        var userForCreation = new FakeUserForCreation().RuleFor(u => u.TenantId, tenant.Id);
        if (identifier is not null)
            userForCreation.RuleFor(u => u.Identifier, identifier);
        var user = User.Create(userForCreation.Generate());
        await InsertAsync(tenant);
        await InsertAsync(user);
        return (tenant, user);
    }
}
