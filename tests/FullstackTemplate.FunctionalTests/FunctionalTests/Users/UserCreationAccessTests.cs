namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class UserCreationAccessTests : TestBase
{
    [Fact]
    public async Task user_with_no_user_record_cannot_add_themselves_to_a_tenant()
    {
        // Arrange
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        await InsertAsync(tenant);
        var identifier = $"outsider-{Guid.NewGuid()}";
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = identifier });
        var dto = new FakeUserForCreationDto()
            .RuleFor(x => x.TenantId, _ => tenant.Id)
            .RuleFor(x => x.Identifier, _ => identifier)
            .Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        await ShouldBeForbiddenAsync(result, "You can add a user only to your own tenant.");
    }

    [Fact]
    public async Task user_cannot_add_a_user_to_another_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        var (otherTenant, _) = await AddTenantWithUserAsync();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });
        var dto = new FakeUserForCreationDto().RuleFor(x => x.TenantId, _ => otherTenant.Id).Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        await ShouldBeForbiddenAsync(result, "You can add a user only to your own tenant.");
    }

    [Fact]
    public async Task user_can_add_a_user_to_their_own_tenant()
    {
        // Arrange
        var (tenant, user) = await AddTenantWithUserAsync();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });
        var dto = new FakeUserForCreationDto().RuleFor(x => x.TenantId, _ => tenant.Id).Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task user_cannot_start_a_tenant_for_another_identifier()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = $"new-{Guid.NewGuid()}" });
        var dto = new FakeInitiateUserAndTenantDto().Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Initiate, dto);

        // Assert
        await ShouldBeForbiddenAsync(result, "You can start a tenant only for yourself.");
    }

    [Fact]
    public async Task machine_client_not_on_the_allow_list_cannot_start_a_tenant()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["client_id"] = "m2m" });
        var dto = new FakeInitiateUserAndTenantDto().Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Initiate, dto);

        // Assert
        await ShouldBeForbiddenAsync(result, "Only a trusted machine client can start a tenant for another user.");
    }

    [Fact]
    public async Task user_with_no_user_record_can_start_a_tenant_for_themselves()
    {
        // Arrange
        var dto = new FakeInitiateUserAndTenantDto().Generate();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = dto.Identifier });

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Initiate, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task ShouldBeForbiddenAsync(HttpResponseMessage result, string detail)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problem.Detail.ShouldBe(detail);
    }

    private static async Task<(Tenant Tenant, User User)> AddTenantWithUserAsync()
    {
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        var user = User.Create(new FakeUserForCreation().RuleFor(u => u.TenantId, tenant.Id).Generate());
        await InsertAsync(tenant);
        await InsertAsync(user);
        return (tenant, user);
    }
}
