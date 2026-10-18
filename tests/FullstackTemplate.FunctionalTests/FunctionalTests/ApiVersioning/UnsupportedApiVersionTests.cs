namespace FullstackTemplate.FunctionalTests.FunctionalTests.ApiVersioning;

using System.Net;
using Shouldly;
using TestUtilities;

public class UnsupportedApiVersionTests : TestBase
{
    [Theory]
    [InlineData("api/v2/tenants")]
    [InlineData("api/v1.5/tenants")]
    [InlineData("api/v2/weather")]
    public async Task request_for_a_version_that_the_endpoint_does_not_declare_returns_not_found(string route)
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

        // Act
        var result = await FactoryClient.GetRequestAsync(route);

        // Assert
        // The version is a URL segment, so a version that the endpoint does not declare is a URL that does not exist.
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task request_for_a_declared_version_reports_the_supported_versions()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetList);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.Headers.GetValues("api-supported-versions").ShouldBe(["1.0"]);
    }
}
