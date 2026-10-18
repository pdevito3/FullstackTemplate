namespace FullstackTemplate.FunctionalTests.FunctionalTests.Auth;

using System.Net;
using Shouldly;
using TestUtilities;

public class UnauthorizedResponseTests : TestBase
{
    [Fact]
    public async Task request_with_no_token_returns_unauthorized_with_no_body()
    {
        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetList);

        // Assert
        // The OpenAPI document states a 401 with no content, so this test fails when the response gets a body.
        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await result.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }
}
