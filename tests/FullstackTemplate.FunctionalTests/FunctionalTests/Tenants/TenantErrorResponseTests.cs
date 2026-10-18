namespace FullstackTemplate.FunctionalTests.FunctionalTests.Tenants;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;
using WebMotions.Fake.Authentication.JwtBearer;

public class TenantErrorResponseTests : TestBase
{
    [Fact]
    public async Task missing_tenant_returns_not_found_problem()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken("user");

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetRecord(Guid.NewGuid()));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.Detail.ShouldBe("Tenant was not found.");
    }

    [Fact]
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
    }

    [Fact]
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
        problem.Errors.ShouldNotBeEmpty();
    }

    [Fact]
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
        problem.Errors.ShouldContainKey("Name");
    }
}
