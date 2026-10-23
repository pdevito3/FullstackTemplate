namespace FullstackTemplate.FunctionalTests.FunctionalTests.Tenants;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class TenantConcurrencyTests : TestBase
{
    private const string StaleDetail = "This record changed after you loaded it. Reload it, then make your change again.";

    [Test]
    public async Task get_gives_the_version_of_the_tenant()
    {
        // Arrange
        var tenant = await AddTenantAndSignInAsAdminAsync();

        // Act
        var loaded = await GetTenantAsync(tenant.Id);

        // Assert
        loaded.Version.ShouldBe(tenant.Version);
        loaded.Version.ShouldNotBe(0u);
    }

    [Test]
    public async Task put_with_the_loaded_version_gives_a_new_version()
    {
        // Arrange
        var tenant = await AddTenantAndSignInAsAdminAsync();
        var loaded = await GetTenantAsync(tenant.Id);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Tenants.Put(tenant.Id),
            new { name = NewName(), version = loaded.Version });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await result.Content.ReadFromJsonAsync<TenantDto>()).ShouldNotBeNull();
        updated.Version.ShouldNotBe(loaded.Version);
        (await GetTenantAsync(tenant.Id)).Version.ShouldBe(updated.Version);
    }

    [Test]
    public async Task put_with_a_stale_version_is_a_stale_version_problem_on_the_version()
    {
        // Arrange
        var tenant = await AddTenantAndSignInAsAdminAsync();
        var loaded = await GetTenantAsync(tenant.Id);
        var name = NewName();
        (await FactoryClient.PutJsonRequestAsync(ApiRoutes.Tenants.Put(tenant.Id),
            new { name, version = loaded.Version })).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Tenants.Put(tenant.Id),
            new { name = NewName(), version = loaded.Version });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Type.ShouldBe(DomainProblemDetails.StaleVersionType);
        problem.Title.ShouldBe("The record changed after you loaded it.");
        problem.Detail.ShouldBe(StaleDetail);
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("version");
        (await GetTenantAsync(tenant.Id)).Name.ShouldBe(name);
    }

    [Test]
    public async Task put_with_a_stale_version_and_a_taken_name_is_a_stale_version_problem()
    {
        // Arrange
        var tenant = await AddTenantAndSignInAsAdminAsync();
        var other = Tenant.Create(new FakeTenantForCreation().Generate());
        await InsertAsync(other);
        var loaded = await GetTenantAsync(tenant.Id);
        (await FactoryClient.PutJsonRequestAsync(ApiRoutes.Tenants.Put(tenant.Id),
            new { name = NewName(), version = loaded.Version })).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Tenants.Put(tenant.Id),
            new { name = other.Name, version = loaded.Version });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Type.ShouldBe(DomainProblemDetails.StaleVersionType);
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("version");
    }

    [Test]
    public async Task put_with_no_version_is_a_bad_request_on_the_version()
    {
        // Arrange
        var tenant = await AddTenantAndSignInAsAdminAsync();

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Tenants.Put(tenant.Id), new { name = NewName() });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Detail.ShouldBe("The version field is required.");
        var error = problem.Errors.ShouldHaveSingleItem();
        error.Key.ShouldBe("version");
        error.Value.ShouldBe(["The version field is required."]);
        (await GetTenantAsync(tenant.Id)).Name.ShouldBe(tenant.Name);
    }

    [Test]
    public async Task two_parallel_puts_from_one_version_give_one_success_and_one_stale_version()
    {
        // Arrange
        var tenant = await AddTenantAndSignInAsAdminAsync();
        var loaded = await GetTenantAsync(tenant.Id);

        // Act
        var results = await Task.WhenAll(
            FactoryClient.PutJsonRequestAsync(ApiRoutes.Tenants.Put(tenant.Id), new { name = NewName(), version = loaded.Version }),
            FactoryClient.PutJsonRequestAsync(ApiRoutes.Tenants.Put(tenant.Id), new { name = NewName(), version = loaded.Version }));

        // Assert
        results.Select(r => r.StatusCode).Order().ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        var problem = (await results.Single(r => r.StatusCode == HttpStatusCode.Conflict).Content
            .ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Type.ShouldBe(DomainProblemDetails.StaleVersionType);
    }

    private async Task<Tenant> AddTenantAndSignInAsAdminAsync()
    {
        await StartGraph()
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .BuildAsync();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = admin.Identifier });
        return tenant;
    }

    private async Task<TenantDto> GetTenantAsync(string id)
    {
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetRecord(id));
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await result.Content.ReadFromJsonAsync<TenantDto>()).ShouldNotBeNull();
    }

    private static string NewName() => $"Tenant {Guid.NewGuid():N}";
}
