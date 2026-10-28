namespace FullstackTemplate.FunctionalTests.FunctionalTests.Auth;

using System.Net;
using System.Text.Json;
using Shouldly;

public class McpResourceMetadataTests : TestBase
{
    [Test]
    public async Task the_metadata_of_mcp_names_the_resource_the_issuer_and_the_scopes_with_no_token()
    {
        // Act
        using var response = await FactoryClient.GetAsync("/.well-known/oauth-protected-resource/mcp");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("resource").GetString().ShouldBe($"{TestingWebApplicationFactory.McpPublicOrigin}/mcp");
        Strings(root.GetProperty("authorization_servers")).ShouldBe(["https://localhost"]);
        Strings(root.GetProperty("scopes_supported")).ShouldBe(["openid", "profile", "test-api"]);
    }

    private static List<string?> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()).ToList();
}
