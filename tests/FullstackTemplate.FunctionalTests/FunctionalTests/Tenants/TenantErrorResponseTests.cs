namespace FullstackTemplate.FunctionalTests.FunctionalTests.Tenants;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using FullstackTemplate.Server.Resources.Entrypoints;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class TenantErrorResponseTests : TestBase
{
    [Test]
    public async Task missing_tenant_returns_not_found_problem()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken("user");

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetRecord(EntityId.New(Tenant.IdPrefix)));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.Detail.ShouldBe("Tenant was not found.");
    }

    [Test]
    public async Task invalid_tenant_returns_validation_problem()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Tenants.Create, new { name = "" });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        problem.Detail.ShouldBe("Please provide a tenant name.");
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("name");
    }

    [Test]
    public async Task malformed_json_returns_bad_request_problem()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken("user");
        using var content = new StringContent("{ \"name\": ", Encoding.UTF8, "application/json");

        // Act
        var result = await FactoryClient.PostAsync(ApiRoutes.Tenants.Create, content);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("$.name");
    }

    [Test]
    [Arguments("")]
    [Arguments("null")]
    public async Task missing_body_returns_bad_request_problem_with_the_empty_key(string body)
    {
        // Arrange
        FactoryClient.SetFakeBearerToken("user");
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        // Act
        var result = await FactoryClient.PostAsync(ApiRoutes.Tenants.Create, content);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Detail.ShouldBe("A non-empty request body is required.");
        var error = problem.Errors.ShouldHaveSingleItem();
        error.Key.ShouldBe("");
        error.Value.ShouldBe(["A non-empty request body is required."]);
    }

    [Test]
    public async Task duplicate_tenant_name_returns_conflict_problem()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });
        var name = $"Tenant {Guid.NewGuid():N}";
        (await FactoryClient.PostJsonRequestAsync(ApiRoutes.Tenants.Create, new { name })).StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Tenants.Create, new { name });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status409Conflict);
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc9110#section-15.5.10");
        problem.Type.ShouldNotBe(DomainProblemDetails.StaleVersionType);
        problem.Title.ShouldBe("The request conflicts with a record that exists.");
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("name");
    }

    [Test]
    public async Task missing_required_property_returns_bad_request_problem_with_errors()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken("user");

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Tenants.Create, new { });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Errors.ShouldContainKey("name");
        // The message names the field the same as the JSON body does
        problem.Errors["name"].ShouldBe(["The name field is required."]);
    }
}
