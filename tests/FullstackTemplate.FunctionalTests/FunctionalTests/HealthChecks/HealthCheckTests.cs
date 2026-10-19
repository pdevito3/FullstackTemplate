namespace FullstackTemplate.FunctionalTests.FunctionalTests.HealthChecks;

using System.Net;
using Shouldly;
using TestUtilities;

public class HealthCheckTests : TestBase
{
    [Test]
    public async Task health_check_returns_ok()
    {
        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Health);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task alive_check_returns_ok()
    {
        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Alive);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
