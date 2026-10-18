namespace FullstackTemplate.FunctionalTests.FunctionalTests.Pagination;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class QueryInputErrorTests : TestBase
{
    [Theory]
    [InlineData("filters", "Name @= ")]
    [InlineData("filters", "Unknown == \"x\"")]
    [InlineData("filters", "Id == \"notaguid\"")]
    [InlineData("sortOrder", "Unknown desc,,")]
    public async Task query_value_that_cannot_be_read_returns_bad_request_problem(string parameter, string value)
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Tenants.GetList}?{parameter}={Uri.EscapeDataString(value)}");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe(parameter);
    }
}
