namespace FullstackTemplate.FunctionalTests.FunctionalTests.Tenants;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Shouldly;
using TestUtilities;

/// <summary>
/// The server caches the tenant of each user. These tests check that a change to a user or a tenant
/// shows on the next request, also when an earlier request put the old result in the cache.
/// </summary>
public class TenantLookupCacheTests : TestBase
{
    [Fact]
    public async Task user_of_a_deleted_tenant_cannot_read_tenant_data()
    {
        // Arrange
        await StartGraph()
            .AddTenant(out var tenant)
            .AddUsers(out var users, 2)
            .BuildAsync();
        var (owner, member) = (users[0], users[1]);
        SignInAs(member);
        (await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetRecord(member.Id))).StatusCode.ShouldBe(HttpStatusCode.OK);

        SignInAs(owner);
        (await FactoryClient.DeleteRequestAsync(ApiRoutes.Tenants.Delete(tenant.Id))).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        // Act
        SignInAs(member);
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetRecord(member.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task user_of_a_deleted_tenant_with_no_cached_tenant_cannot_read_tenant_data()
    {
        // Arrange
        await StartGraph()
            .AddTenant(out var tenant)
            .AddUsers(out var users, 2)
            .BuildAsync();
        var (owner, member) = (users[0], users[1]);
        SignInAs(owner);
        (await FactoryClient.DeleteRequestAsync(ApiRoutes.Tenants.Delete(tenant.Id))).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        // Act
        SignInAs(member);
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetRecord(member.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task user_who_starts_a_tenant_after_a_cached_miss_can_get_the_tenant()
    {
        // Arrange
        var dto = new FakeInitiateUserAndTenantDto().Generate();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = dto.Identifier });
        var miss = await FactoryClient.GetRequestAsync($"{ApiRoutes.Tenants.GetList}?pageSize=500");
        (await miss.Content.ReadFromJsonAsync<List<object>>()).ShouldNotBeNull().ShouldBeEmpty();

        var initiated = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Initiate, dto);
        initiated.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tenantId = (await initiated.Content.ReadFromJsonAsync<InitiateUserAndTenantResponseDto>())
            .ShouldNotBeNull().Tenant.Id;

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetRecord(tenantId));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task deleted_user_cannot_read_their_tenant()
    {
        // Arrange
        await StartGraph()
            .AddTenant(out var tenant)
            .AddUsers(out var users, 2)
            .BuildAsync();
        var (admin, removed) = (users[0], users[1]);
        SignInAs(removed);
        (await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetRecord(tenant.Id))).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        SignInAs(admin);
        (await FactoryClient.DeleteRequestAsync(ApiRoutes.Users.Delete(removed.Id))).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        // Act
        SignInAs(removed);
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetRecord(tenant.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private void SignInAs(User user)
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });
}
