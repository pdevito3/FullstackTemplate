namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class UserErrorResponseTests : TestBase
{
    [Test]
    public async Task blank_email_on_create_returns_validation_problem()
    {
        // Arrange
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = "Admin");
        await InsertAsync(tenant);
        await InsertAsync(user);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });
        var dto = new FakeUserForCreationDto()
            .RuleFor(x => x.Email, _ => "")
            .Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        await ShouldBeBlankEmailProblem(result);
    }

    [Test]
    public async Task blank_email_on_update_returns_validation_problem()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant).AddUser(out var user);
        await InsertAsync(tenant);
        await InsertAsync(user);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });
        var dto = new FakeUserForUpdateDto()
            .RuleFor(x => x.Email, _ => "")
            .Generate();

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(user.Id), dto);

        // Assert
        await ShouldBeBlankEmailProblem(result);
    }

    private static async Task ShouldBeBlankEmailProblem(HttpResponseMessage result)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        problem.Detail.ShouldBe("Please provide a valid email.");
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("email");
    }

    [Test]
    [Arguments("firstName")]
    [Arguments("lastName")]
    [Arguments("identifier")]
    [Arguments("username")]
    public async Task blank_field_on_create_returns_validation_problem_keyed_by_the_field(string field)
    {
        // Arrange
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = "Admin");
        await InsertAsync(tenant);
        await InsertAsync(user);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });
        var valid = new FakeUserForCreationDto().Generate();
        var dto = field switch
        {
            "firstName" => valid with { FirstName = "" },
            "lastName" => valid with { LastName = "" },
            "identifier" => valid with { Identifier = "" },
            _ => valid with { Username = "" },
        };

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe(field);
    }

    [Test]
    public async Task blank_tenant_name_on_initiate_returns_validation_problem_keyed_by_the_field()
    {
        // Arrange
        var dto = new FakeInitiateUserAndTenantDto().RuleFor(x => x.TenantName, _ => "").Generate();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = dto.Identifier });

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Initiate, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("tenantName");
    }

    [Test]
    public async Task taken_tenant_name_on_initiate_returns_conflict_keyed_by_the_field()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant);
        await InsertAsync(tenant);
        var dto = new FakeInitiateUserAndTenantDto().RuleFor(x => x.TenantName, _ => tenant.Name).Generate();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = dto.Identifier });

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Initiate, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Detail.ShouldBe("A tenant with this name already exists.");
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("tenantName");
    }

    [Test]
    public async Task invalid_email_returns_validation_problem_keyed_by_the_field()
    {
        // Arrange
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = "Admin");
        await InsertAsync(tenant);
        await InsertAsync(user);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });
        var dto = new FakeUserForCreationDto()
            .RuleFor(x => x.Email, _ => "not-an-email")
            .Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Detail.ShouldBe("Please provide a valid email.");
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("email");
        problem.Errors["email"].ShouldBe(["Please provide a valid email."]);
    }

    [Test]
    public async Task unknown_role_returns_validation_problem_keyed_by_the_field()
    {
        // Arrange
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = "Admin");
        await InsertAsync(tenant);
        await InsertAsync(user);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });
        var dto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "NotARole")
            .Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("role");
    }

    [Test]
    public async Task unknown_permission_returns_validation_problem_keyed_by_the_field()
    {
        // Arrange
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = "Admin");
        await InsertAsync(tenant);
        await InsertAsync(user);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.AddPermission(user.Id), new { permission = "NotAPermission" });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("permission");
    }

    [Test]
    public async Task unknown_identifier_returns_not_found_problem()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken("user");

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetByIdentifier($"unknown-{Guid.NewGuid()}"));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
    }

    public static IEnumerable<string> BadTenantIds =>
    [
        "ten_doesnotexist",
        "ten_" + new string('a', 76),
        EntityId.New(User.IdPrefix),
    ];

    [Test]
    [MethodDataSource(nameof(BadTenantIds))]
    public async Task bad_or_unknown_tenant_id_returns_validation_problem(string tenantId)
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });
        var dto = new FakeUserForCreationDto().RuleFor(x => x.TenantId, _ => tenantId).Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        problem.Detail.ShouldBe("The tenant does not exist.");
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("tenantId");
    }
}
