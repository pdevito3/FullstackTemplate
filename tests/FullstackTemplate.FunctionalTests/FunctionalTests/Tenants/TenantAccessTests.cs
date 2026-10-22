namespace FullstackTemplate.FunctionalTests.FunctionalTests.Tenants;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using Microsoft.AspNetCore.Http;
using FullstackTemplate.Server.Resources.Entrypoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using TestUtilities;

public class TenantAccessTests : TestBase
{
    [Test]
    public async Task user_can_get_their_own_tenant()
    {
        // Arrange
        var (tenant, user) = await AddTenantWithUserAsync("User");
        SignInAs(user);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetRecord(tenant.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadFromJsonAsync<TenantDto>()).ShouldNotBeNull().Id.ShouldBe(tenant.Id);
    }

    [Test]
    public async Task user_cannot_get_another_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync("User");
        var (otherTenant, _) = await AddTenantWithUserAsync("User");
        SignInAs(user);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetRecord(otherTenant.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task user_sees_only_their_own_tenant_in_the_list()
    {
        // Arrange
        var (tenant, user) = await AddTenantWithUserAsync("User");
        await AddTenantWithUserAsync("User");
        SignInAs(user);

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Tenants.GetList}?pageSize=500");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenants = (await result.Content.ReadFromJsonAsync<List<TenantDto>>()).ShouldNotBeNull();
        tenants.ShouldHaveSingleItem().Id.ShouldBe(tenant.Id);
    }

    [Test]
    public async Task user_with_no_user_record_sees_no_tenants()
    {
        // Arrange
        await AddTenantWithUserAsync("User");
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = $"unknown-{Guid.NewGuid()}" });

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Tenants.GetList}?pageSize=500");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadFromJsonAsync<List<TenantDto>>()).ShouldNotBeNull().ShouldBeEmpty();
    }

    [Test]
    public async Task admin_cannot_update_another_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync("Admin");
        var (otherTenant, _) = await AddTenantWithUserAsync("User");
        SignInAs(user);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(
            ApiRoutes.Tenants.Put(otherTenant.Id), new { name = $"taken-{Guid.NewGuid()}", version = otherTenant.Version });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var name = await ExecuteDbContextAsync(db => db.Tenants
            .Where(t => t.Id == otherTenant.Id).Select(t => t.Name).SingleAsync());
        name.ShouldBe(otherTenant.Name);
    }

    [Test]
    public async Task admin_cannot_delete_another_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync("Admin");
        var (otherTenant, _) = await AddTenantWithUserAsync("User");
        SignInAs(user);

        // Act
        var result = await FactoryClient.DeleteRequestAsync(ApiRoutes.Tenants.Delete(otherTenant.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var exists = await ExecuteDbContextAsync(db => db.Tenants.AnyAsync(t => t.Id == otherTenant.Id));
        exists.ShouldBeTrue();
    }

    [Test]
    public async Task basic_member_cannot_update_their_own_tenant()
    {
        // Arrange
        var (tenant, member) = await AddTenantWithUserAsync("User");
        SignInAs(member);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(
            ApiRoutes.Tenants.Put(tenant.Id), new { name = $"renamed-{Guid.NewGuid()}", version = tenant.Version });

        // Assert
        await ShouldBeForbiddenAsync(result, "Only an admin can do this.");
        var name = await ExecuteDbContextAsync(db => db.Tenants
            .Where(t => t.Id == tenant.Id).Select(t => t.Name).SingleAsync());
        name.ShouldBe(tenant.Name);
    }

    [Test]
    public async Task admin_can_update_their_own_tenant()
    {
        // Arrange
        var (tenant, admin) = await AddTenantWithUserAsync("Admin");
        SignInAs(admin);
        var newName = $"renamed-{Guid.NewGuid()}";

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Tenants.Put(tenant.Id), new { name = newName, version = tenant.Version });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadFromJsonAsync<TenantDto>()).ShouldNotBeNull().Name.ShouldBe(newName);
    }

    [Test]
    public async Task admin_can_keep_the_name_of_their_own_tenant()
    {
        // Arrange
        var (tenant, admin) = await AddTenantWithUserAsync("Admin");
        SignInAs(admin);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Tenants.Put(tenant.Id), new { name = tenant.Name, version = tenant.Version });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task admin_cannot_rename_their_tenant_to_the_name_of_another_tenant()
    {
        // Arrange
        var (tenant, admin) = await AddTenantWithUserAsync("Admin");
        var (otherTenant, _) = await AddTenantWithUserAsync("User");
        SignInAs(admin);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Tenants.Put(tenant.Id), new { name = otherTenant.Name, version = tenant.Version });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status409Conflict);
        problem.Type.ShouldNotBe(DomainProblemDetails.StaleVersionType);
        problem.Title.ShouldBe("The request conflicts with a record that exists.");
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("name");
    }

    [Test]
    public async Task basic_member_cannot_delete_their_own_tenant()
    {
        // Arrange
        var (tenant, member) = await AddTenantWithUserAsync("User");
        SignInAs(member);

        // Act
        var result = await FactoryClient.DeleteRequestAsync(ApiRoutes.Tenants.Delete(tenant.Id));

        // Assert
        await ShouldBeForbiddenAsync(result, "Only an admin can do this.");
        var exists = await ExecuteDbContextAsync(db => db.Tenants.AnyAsync(t => t.Id == tenant.Id));
        exists.ShouldBeTrue();
    }

    [Test]
    public async Task user_cannot_add_a_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync("User");
        SignInAs(user);

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(
            ApiRoutes.Tenants.Create, new FakeTenantForCreationDto().Generate());

        // Assert
        await ShouldBeForbiddenAsync(result, AddTenantForbiddenDetail);
    }

    [Test]
    public async Task trusted_machine_client_can_add_and_get_any_tenant()
    {
        // Arrange
        var (otherTenant, _) = await AddTenantWithUserAsync("User");
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

    [Test]
    public async Task read_only_machine_client_can_read_all_tenants()
    {
        // Arrange
        var (firstTenant, _) = await AddTenantWithUserAsync("User");
        var (secondTenant, _) = await AddTenantWithUserAsync("User");
        SignInAsReadOnlyMachineClient();

        // Act
        var record = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetRecord(firstTenant.Id));
        var list = await FactoryClient.GetRequestAsync($"{ApiRoutes.Tenants.GetList}?pageSize=500");

        // Assert
        record.StatusCode.ShouldBe(HttpStatusCode.OK);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ids = (await list.Content.ReadFromJsonAsync<List<TenantDto>>()).ShouldNotBeNull().Select(t => t.Id);
        ids.ShouldContain(firstTenant.Id);
        ids.ShouldContain(secondTenant.Id);
    }

    [Test]
    public async Task read_only_machine_client_cannot_add_a_tenant()
    {
        // Arrange
        SignInAsReadOnlyMachineClient();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(
            ApiRoutes.Tenants.Create, new FakeTenantForCreationDto().Generate());

        // Assert
        await ShouldBeForbiddenAsync(result, AddTenantForbiddenDetail);
    }

    [Test]
    public async Task read_only_machine_client_cannot_change_a_tenant()
    {
        // Arrange
        var (tenant, _) = await AddTenantWithUserAsync("User");
        SignInAsReadOnlyMachineClient();

        // Act
        var updated = await FactoryClient.PutJsonRequestAsync(
            ApiRoutes.Tenants.Put(tenant.Id), new { name = $"renamed-{Guid.NewGuid()}", version = tenant.Version });
        var deleted = await FactoryClient.DeleteRequestAsync(ApiRoutes.Tenants.Delete(tenant.Id));

        // Assert
        await ShouldBeForbiddenAsync(updated, "Only an admin can do this.");
        await ShouldBeForbiddenAsync(deleted, "Only an admin can do this.");
        var exists = await ExecuteDbContextAsync(db => db.Tenants.AnyAsync(t => t.Id == tenant.Id));
        exists.ShouldBeTrue();
    }

    private const string AddTenantForbiddenDetail =
        "Only a trusted tenant management client can add a tenant. A user starts their own tenant with POST /api/v1/users/initiate.";

    private void SignInAsReadOnlyMachineClient()
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.ReadOnlyMachineClientId
        });

    private void SignInAs(User user)
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });

    private static async Task ShouldBeForbiddenAsync(HttpResponseMessage result, string detail)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problem.Detail.ShouldBe(detail);
    }

    private static async Task<(Tenant Tenant, User User)> AddTenantWithUserAsync(string role)
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = role);
        await InsertAsync(tenant);
        await InsertAsync(user);
        return (tenant, user);
    }
}
