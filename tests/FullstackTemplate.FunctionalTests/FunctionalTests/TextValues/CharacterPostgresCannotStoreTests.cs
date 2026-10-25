namespace FullstackTemplate.FunctionalTests.FunctionalTests.TextValues;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Agent;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using TestUtilities;

public class CharacterPostgresCannotStoreTests : TestBase
{
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Test]
    public async Task a_nul_in_a_first_name_returns_a_validation_problem_for_the_field_and_saves_no_user()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant).AddUser(out var admin, u => u.Role = "Admin");
        await InsertAsync(tenant);
        await InsertAsync(admin);
        FactoryClient.SignInAsAppUser(admin.Identifier);
        var dto = new FakeUserForCreationDto().Generate();
        var body = Json(dto);
        body["firstName"] = "Ada\0";

        // Act
        var result = await FactoryClient.PostAsync(ApiRoutes.Users.Create, JsonContent.Create(body));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        problem.Detail.ShouldBe("A first name has a character that is not allowed.");
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("firstName");
        problem.Errors["firstName"].ShouldBe(["A first name has a character that is not allowed."]);
        var saved = await ExecuteDbContextAsync(db => db.Users.IgnoreQueryFilters().AnyAsync(u => u.Identifier == dto.Identifier));
        saved.ShouldBeFalse();
    }

    public static IEnumerable<(string, string)> WriteEndpointTextFields =>
    [
        ("add user", "firstName"),
        ("add user", "lastName"),
        ("add user", "identifier"),
        ("add user", "email"),
        ("add user", "username"),
        ("update user", "firstName"),
        ("update user", "lastName"),
        ("update user", "email"),
        ("update user", "username"),
        ("initiate user and tenant", "tenantName"),
        ("initiate user and tenant", "firstName"),
        ("initiate user and tenant", "lastName"),
        ("initiate user and tenant", "identifier"),
        ("initiate user and tenant", "email"),
        ("initiate user and tenant", "username"),
        ("add tenant", "name"),
        ("update tenant", "name"),
        ("add agent", "name"),
        ("add agent", "clientId"),
    ];

    [Test]
    [MethodDataSource(nameof(WriteEndpointTextFields))]
    public async Task a_nul_in_a_text_field_of_a_write_endpoint_returns_a_validation_problem_for_the_field(string endpoint, string field)
    {
        // Arrange
        var (method, url, body) = await ArrangeAsync(endpoint);
        body[field] = field == "email" ? "a\0b@example.com" : "a\0b";

        // Act
        var result = await FactoryClient.SendAsync(new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe(field);
        problem.Detail.ShouldNotBeNull().ShouldEndWith("has a character that is not allowed.");
    }

    [Test]
    [Arguments("add user")]
    [Arguments("add agent")]
    public async Task a_nul_in_the_tenant_id_returns_a_validation_problem_for_the_field(string endpoint)
    {
        // Arrange
        var (method, url, body) = await ArrangeAsync(endpoint);
        SignInAsTrustedMachineClient();
        body["tenantId"] = "ten_\0";

        // Act
        var result = await FactoryClient.SendAsync(new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("tenantId");
    }

    private async Task<(HttpMethod Method, string Url, JsonObject Body)> ArrangeAsync(string endpoint)
    {
        TestGraph.Start(out _).AddTenant(out var tenant).AddUser(out var admin, u => u.Role = "Admin");
        await InsertAsync(tenant);
        await InsertAsync(admin);
        FactoryClient.SignInAsAppUser(admin.Identifier);

        switch (endpoint)
        {
            case "add user":
                return (HttpMethod.Post, ApiRoutes.Users.Create, Json(new FakeUserForCreationDto().Generate()));
            case "update user":
                return (HttpMethod.Put, ApiRoutes.Users.Put(admin.Id),
                    Json(new FakeUserForUpdateDto().Generate() with { Version = admin.Version }));
            case "initiate user and tenant":
                SignInAsTrustedMachineClient();
                return (HttpMethod.Post, ApiRoutes.Users.Initiate, Json(new FakeInitiateUserAndTenantDto().Generate()));
            case "add tenant":
                SignInAsTrustedMachineClient();
                return (HttpMethod.Post, ApiRoutes.Tenants.Create, Json(new FakeTenantForCreationDto().Generate()));
            case "update tenant":
                return (HttpMethod.Put, ApiRoutes.Tenants.Put(tenant.Id),
                    Json(new TenantForUpdateDto { Name = $"Tenant {Guid.NewGuid()}", Version = tenant.Version }));
            case "add agent":
                return (HttpMethod.Post, ApiRoutes.Agents.Create, Json(new FakeAgentForCreationDto().Generate()));
            default:
                throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null);
        }
    }

    private void SignInAsTrustedMachineClient()
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

    private static JsonObject Json<T>(T dto) => JsonSerializer.SerializeToNode(dto, CamelCase)!.AsObject();
}
