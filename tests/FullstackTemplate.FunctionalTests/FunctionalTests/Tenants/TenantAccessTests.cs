namespace FullstackTemplate.FunctionalTests.FunctionalTests.Tenants;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using TestUtilities;

public class TenantAccessTests : TestBase
{
    [Fact]
    public async Task user_can_get_their_own_tenant()
    {
        // Arrange
        var (tenant, user) = await AddTenantWithUserAsync();
        SignInAs(user);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetRecord(tenant.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadFromJsonAsync<TenantDto>()).ShouldNotBeNull().Id.ShouldBe(tenant.Id);
    }

    [Fact]
    public async Task user_cannot_get_another_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        var (otherTenant, _) = await AddTenantWithUserAsync();
        SignInAs(user);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetRecord(otherTenant.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task user_sees_only_their_own_tenant_in_the_list()
    {
        // Arrange
        var (tenant, user) = await AddTenantWithUserAsync();
        await AddTenantWithUserAsync();
        SignInAs(user);

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Tenants.GetList}?pageSize=500");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenants = (await result.Content.ReadFromJsonAsync<List<TenantDto>>()).ShouldNotBeNull();
        tenants.ShouldHaveSingleItem().Id.ShouldBe(tenant.Id);
    }

    [Fact]
    public async Task user_with_no_user_record_sees_no_tenants()
    {
        // Arrange
        await AddTenantWithUserAsync();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = $"unknown-{Guid.NewGuid()}" });

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Tenants.GetList}?pageSize=500");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadFromJsonAsync<List<TenantDto>>()).ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task user_cannot_update_another_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        var (otherTenant, _) = await AddTenantWithUserAsync();
        SignInAs(user);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(
            ApiRoutes.Tenants.Put(otherTenant.Id), new { name = $"taken-{Guid.NewGuid()}" });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var name = await ExecuteDbContextAsync(db => db.Tenants
            .Where(t => t.Id == otherTenant.Id).Select(t => t.Name).SingleAsync());
        name.ShouldBe(otherTenant.Name);
    }

    [Fact]
    public async Task user_cannot_delete_another_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        var (otherTenant, _) = await AddTenantWithUserAsync();
        SignInAs(user);

        // Act
        var result = await FactoryClient.DeleteRequestAsync(ApiRoutes.Tenants.Delete(otherTenant.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var exists = await ExecuteDbContextAsync(db => db.Tenants.AnyAsync(t => t.Id == otherTenant.Id));
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task user_cannot_add_a_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        SignInAs(user);

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(
            ApiRoutes.Tenants.Create, new FakeTenantForCreationDto().Generate());

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problem.Detail.ShouldBe("Only a trusted machine client can add a tenant.");
    }

    [Fact]
    public async Task trusted_machine_client_can_add_and_get_any_tenant()
    {
        // Arrange
        var (otherTenant, _) = await AddTenantWithUserAsync();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

        // Act
        var added = await FactoryClient.PostJsonRequestAsync(
            ApiRoutes.Tenants.Create, new FakeTenantForCreationDto().Generate());
        var other = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetRecord(otherTenant.Id));

        // Assert
        added.StatusCode.ShouldBe(HttpStatusCode.Created);
        other.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private void SignInAs(User user)
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });

    private static async Task<(Tenant Tenant, User User)> AddTenantWithUserAsync()
    {
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        var user = User.Create(new FakeUserForCreation().RuleFor(u => u.TenantId, tenant.Id).Generate());
        await InsertAsync(tenant);
        await InsertAsync(user);
        return (tenant, user);
    }
}
