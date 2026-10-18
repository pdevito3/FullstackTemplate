namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using TestUtilities;

public class UserCreationAccessTests : TestBase
{
    [Fact]
    public async Task user_with_no_user_record_cannot_add_themselves_to_a_tenant()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant);
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
        await ShouldBeForbiddenAsync(result, "Only an admin can do this.");
    }

    [Fact]
    public async Task admin_cannot_add_a_user_to_another_tenant()
    {
        // Arrange
        var (_, admin) = await AddTenantWithUserAsync("Admin");
        var (otherTenant, _) = await AddTenantWithUserAsync("Admin");
        SignInAs(admin);
        var dto = new FakeUserForCreationDto().RuleFor(x => x.TenantId, _ => otherTenant.Id).Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        await ShouldBeForbiddenAsync(result, "You can add a user only to your own tenant.");
    }

    [Fact]
    public async Task admin_adds_a_user_to_their_own_tenant()
    {
        // Arrange
        var (tenant, admin) = await AddTenantWithUserAsync("Admin");
        SignInAs(admin);
        var dto = new FakeUserForCreationDto().Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Created);
        var user = (await result.Content.ReadFromJsonAsync<UserDto>()).ShouldNotBeNull();
        user.TenantId.ShouldBe(tenant.Id);
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
        await ShouldBeForbiddenAsync(result,
            "A caller with no user identifier must be a machine client on the all-tenant allow-list to start a tenant.");
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

    [Fact]
    public async Task basic_member_cannot_add_a_user()
    {
        // Arrange
        var (_, member) = await AddTenantWithUserAsync("User");
        SignInAs(member);
        var dto = new FakeUserForCreationDto().RuleFor(x => x.Role, _ => "Admin").Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        await ShouldBeForbiddenAsync(result, "Only an admin can do this.");
        var added = await ExecuteDbContextAsync(db => db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Identifier == dto.Identifier));
        added.ShouldBeFalse();
    }

    [Fact]
    public async Task basic_member_cannot_change_a_role()
    {
        // Arrange
        var (_, member) = await AddTenantWithUserAsync("User");
        SignInAs(member);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Role(member.Id), new { role = "Admin" });

        // Assert
        await ShouldBeForbiddenAsync(result, "Only an admin can do this.");
    }

    [Fact]
    public async Task duplicate_identifier_returns_validation_problem()
    {
        // Arrange
        var (_, admin) = await AddTenantWithUserAsync("Admin");
        SignInAs(admin);
        var dto = new FakeUserForCreationDto().RuleFor(x => x.Identifier, _ => admin.Identifier).Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        problem.Detail.ShouldBe("A user with this identifier already exists.");
        problem.Errors.ShouldContainKey(nameof(UserForCreationDto.Identifier));
    }

    [Fact]
    public async Task concurrent_adds_with_one_identifier_add_one_user_and_reject_the_others()
    {
        // Arrange
        var (_, admin) = await AddTenantWithUserAsync("Admin");
        SignInAs(admin);
        var identifier = Guid.NewGuid().ToString();
        var dtos = Enumerable.Range(0, 5)
            .Select(_ => new FakeUserForCreationDto().RuleFor(x => x.Identifier, _ => identifier).Generate())
            .ToList();

        // Act
        var results = await Task.WhenAll(dtos.Select(dto => FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto)));

        // Assert
        // The duplicate check and the unique index can each stop a request, and both must give 422, never 500.
        results.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        results.Where(r => r.StatusCode != HttpStatusCode.Created)
            .ShouldAllBe(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task trusted_machine_client_can_add_a_user_to_a_tenant_that_it_names()
    {
        // Arrange
        var (tenant, _) = await AddTenantWithUserAsync("Admin");
        SignInAsTrustedMachineClient();
        var dto = new FakeUserForCreationDto().RuleFor(x => x.TenantId, _ => tenant.Id).Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Created);
        var user = (await result.Content.ReadFromJsonAsync<UserDto>()).ShouldNotBeNull();
        user.TenantId.ShouldBe(tenant.Id);
    }

    [Fact]
    public async Task trusted_machine_client_must_name_a_tenant_to_add_a_user()
    {
        // Arrange
        SignInAsTrustedMachineClient();
        var dto = new FakeUserForCreationDto().Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldContainKey(nameof(UserForCreationDto.TenantId));
    }

    [Fact]
    public async Task trusted_machine_client_can_start_a_tenant_for_another_identifier()
    {
        // Arrange
        SignInAsTrustedMachineClient();
        var dto = new FakeInitiateUserAndTenantDto().Generate();

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

    private void SignInAs(User user)
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });

    private void SignInAsTrustedMachineClient()
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

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
