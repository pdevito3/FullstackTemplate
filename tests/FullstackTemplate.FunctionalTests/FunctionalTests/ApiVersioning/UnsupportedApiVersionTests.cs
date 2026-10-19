namespace FullstackTemplate.FunctionalTests.FunctionalTests.ApiVersioning;

using System.Net;
using Shouldly;
using TestUtilities;

public class UnsupportedApiVersionTests : TestBase
{
    [Theory]
    [InlineData("api/v1.5/weather")]
    [InlineData("api/v2/weather")]
    public async Task request_for_a_version_that_the_endpoint_does_not_declare_returns_not_found(string route)
    {
        // Act
        var result = await FactoryClient.GetRequestAsync(route);

        // Assert
        // The version is a URL segment, so a version that the endpoint does not declare is a URL that does not exist.
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task request_for_a_declared_version_reports_the_supported_versions()
    {
        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Weather.Get);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.Headers.GetValues("api-supported-versions").ShouldBe(["1.0"]);
    }
}
