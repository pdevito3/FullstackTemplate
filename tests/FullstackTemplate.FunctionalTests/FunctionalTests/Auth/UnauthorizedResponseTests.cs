namespace FullstackTemplate.FunctionalTests.FunctionalTests.Auth;

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using OpenApi;
using Shouldly;
using TestUtilities;

public partial class UnauthorizedResponseTests : TestBase
{
    [Test]
    public async Task request_with_no_token_returns_unauthorized_with_no_body()
    {
        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetList);

        // Assert
        // The OpenAPI document states a 401 with no content, so this test fails when the response gets a body.
        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await result.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task each_secured_operation_returns_unauthorized_with_no_body_for_a_request_with_no_token()
    {
        // Arrange
        // The operations come from the OpenAPI document, so a new endpoint is in this test automatically
        var root = await OpenApiDocumentTests.LoadDocumentAsync();
        var secured = OpenApiDocumentTests.Operations(root)
            .Where(o => o.Name != OpenApiDocumentTests.AnonymousOperation)
            .ToList();
        secured.ShouldNotBeEmpty();
        var failures = new List<string>();

        foreach (var (name, method, operation) in secured)
        {
            var path = name[(method.Length + 1)..];
            using var request = new HttpRequestMessage(new HttpMethod(method), RouteParameter().Replace(path, Guid.NewGuid().ToString()));
            if (operation["requestBody"] is not null)
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            // Act
            using var result = await FactoryClient.SendAsync(request);
            var body = await result.Content.ReadAsByteArrayAsync();

            if (result.StatusCode != HttpStatusCode.Unauthorized || body.Length > 0)
                failures.Add($"{name}: {(int)result.StatusCode} with {body.Length} body bytes");
        }

        // Assert
        failures.ShouldBeEmpty();
    }

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameter();
}
