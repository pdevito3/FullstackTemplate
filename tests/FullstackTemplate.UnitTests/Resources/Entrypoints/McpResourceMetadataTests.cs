namespace FullstackTemplate.UnitTests.Resources.Entrypoints;

using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.Server.Resources.Entrypoints.Mcp;
using Microsoft.Extensions.Configuration;
using Shouldly;

public class McpResourceMetadataTests
{
    private const string MetadataUrl = "https://api.example.com/.well-known/oauth-protected-resource/mcp";

    [Test]
    public void a_challenge_with_no_error_has_only_the_metadata_url()
    {
        // Act & Assert
        McpResourceMetadata.Challenge(MetadataUrl, null, null, null)
            .ShouldBe($"Bearer resource_metadata=\"{MetadataUrl}\"");
    }

    [Test]
    public void a_challenge_with_an_error_gives_each_error_parameter_after_the_metadata_url()
    {
        // Act & Assert
        McpResourceMetadata.Challenge(MetadataUrl, "invalid_token", "The token expired at '10/28/2026 12:00:00'", "https://errors.example.com/expired")
            .ShouldBe($"Bearer resource_metadata=\"{MetadataUrl}\", error=\"invalid_token\", error_description=\"The token expired at '10/28/2026 12:00:00'\", error_uri=\"https://errors.example.com/expired\"");
    }

    [Test]
    public void a_quote_in_the_description_does_not_end_the_quoted_string()
    {
        // Act & Assert
        McpResourceMetadata.Challenge(MetadataUrl, "invalid_token", "The audience \"x\" is not valid", null)
            .ShouldBe($"Bearer resource_metadata=\"{MetadataUrl}\", error=\"invalid_token\", error_description=\"The audience 'x' is not valid\"");
    }

    [Test]
    public void a_challenge_with_an_error_and_no_description_has_no_empty_parameter()
    {
        // Act & Assert
        McpResourceMetadata.Challenge(MetadataUrl, "invalid_token", "", null)
            .ShouldBe($"Bearer resource_metadata=\"{MetadataUrl}\", error=\"invalid_token\"");
    }

    [Test]
    [Arguments("https://api.example.com", "https://api.example.com")]
    [Arguments("https://api.example.com/", "https://api.example.com")]
    [Arguments("https://API.example.com:443", "https://api.example.com")]
    [Arguments("http://localhost:5123", "http://localhost:5123")]
    public void a_public_origin_gives_the_origin(string value, string origin)
    {
        // Act & Assert
        McpResourceMetadata.ParsePublicOrigin(value).ShouldBe(origin);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("api.example.com")]
    [Arguments("/mcp")]
    [Arguments("ftp://api.example.com")]
    [Arguments("http://api.example.com")]
    [Arguments("https://api.example.com/mcp")]
    [Arguments("https://api.example.com?a=1")]
    [Arguments("https://api.example.com#top")]
    [Arguments("https://user:secret@api.example.com")]
    public void a_value_that_is_not_an_origin_stops_startup(string? value)
    {
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => McpResourceMetadata.ParsePublicOrigin(value))
            .Message.ShouldContain(McpResourceMetadata.PublicOriginKey);
    }

    [Test]
    public void the_scopes_are_openid_profile_and_the_audience_by_default()
    {
        // Act & Assert
        ApiScopes.FromConfiguration(Configuration(("Auth:Audience", "api"))).Keys
            .ShouldBe(["openid", "profile", "api"]);
    }

    [Test]
    public void the_scopes_have_no_audience_when_it_is_not_a_scope()
    {
        // Act & Assert
        ApiScopes.FromConfiguration(Configuration(("Auth:Audience", "api"), ("Auth:UseAudienceAsScope", "false"))).Keys
            .ShouldBe(["openid", "profile"]);
    }

    [Test]
    public void configured_scopes_replace_the_default_scopes()
    {
        // Act & Assert
        ApiScopes.FromConfiguration(Configuration(("Auth:Audience", "api"), ("Auth:Scopes:api.read", "Read the API"))).Keys
            .ShouldBe(["api.read"]);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, (string?)v.Value)))
            .Build();
}
