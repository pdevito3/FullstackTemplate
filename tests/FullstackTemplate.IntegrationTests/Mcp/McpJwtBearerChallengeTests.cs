namespace FullstackTemplate.IntegrationTests.Mcp;

using System.Net;
using System.Security.Cryptography;
using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.Server.Resources.Entrypoints.Mcp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

public class McpJwtBearerChallengeTests : TestBase
{
    private const string Authority = "https://idp.test/realms/app";
    private const string ResourceMetadata = "resource_metadata=\"https://server.test/.well-known/oauth-protected-resource/mcp\"";

    [Test]
    public async Task a_request_to_mcp_with_no_token_gets_the_metadata_url_in_the_first_challenge()
    {
        // Arrange
        await using var app = await StartAsync();

        // Act
        using var response = await app.GetTestClient().PostAsync(EntrypointRegistration.McpPath, null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var challenges = response.Headers.WwwAuthenticate.ToList();
        challenges.Select(c => c.Scheme).ShouldBe(["Bearer", "DPoP"]);
        challenges[0].Parameter.ShouldBe(ResourceMetadata);
        challenges[1].Parameter.ShouldNotBeNull().ShouldStartWith("algs=\"ES256");
    }

    [Test]
    public async Task a_request_to_mcp_with_a_bad_token_gets_the_metadata_url_and_the_error()
    {
        // Arrange
        await using var app = await StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, EntrypointRegistration.McpPath);
        request.Headers.Authorization = new("Bearer", TokenWithUnknownKey());

        // Act
        using var response = await app.GetTestClient().SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var challenges = response.Headers.WwwAuthenticate.ToList();
        challenges.Select(c => c.Scheme).ShouldBe(["Bearer", "DPoP"]);
        var bearer = challenges[0].Parameter.ShouldNotBeNull();
        bearer.ShouldStartWith($"{ResourceMetadata}, error=\"invalid_token\", error_description=\"");
    }

    [Test]
    public async Task a_request_to_mcp_that_fails_a_dpop_check_keeps_only_the_dpop_challenge()
    {
        // Arrange
        await using var app = await StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, EntrypointRegistration.McpPath);
        request.Headers.Authorization = new("DPoP", TokenWithUnknownKey());

        // Act
        using var response = await app.GetTestClient().SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var challenge = response.Headers.WwwAuthenticate.ShouldHaveSingleItem();
        challenge.Scheme.ShouldBe("DPoP");
        challenge.Parameter.ShouldNotBeNull().ShouldStartWith("error=\"invalid_token\"");
    }

    [Test]
    public async Task a_request_to_the_http_api_with_no_token_gets_no_metadata_url()
    {
        // Arrange
        await using var app = await StartAsync();

        // Act
        using var response = await app.GetTestClient().GetAsync("/api/v1/tenants");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var challenges = response.Headers.WwwAuthenticate.ToList();
        challenges.Select(c => c.Scheme).ShouldBe(["DPoP", "Bearer"], ignoreOrder: true);
        challenges.ShouldAllBe(c => c.Parameter == null || !c.Parameter.Contains("resource_metadata"));
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["Auth:Authority"] = Authority;
        builder.Configuration["Auth:Audience"] = "test-api";
        builder.Configuration[McpResourceMetadata.PublicOriginKey] = "https://server.test";
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddJwtBearerAuthentication(builder.Configuration, builder.Environment);
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, UseDiscoveryWithNoSigningKeys);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapMethods("/{**path}", ["GET", "POST"], () => Results.Ok()).RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private static void UseDiscoveryWithNoSigningKeys(JwtBearerOptions options) =>
        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
            new OpenIdConnectConfiguration { Issuer = Authority });

    private static string TokenWithUnknownKey() => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = Authority,
        Audience = "test-api",
        Claims = new Dictionary<string, object> { ["sub"] = "user-1" },
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)), SecurityAlgorithms.HmacSha256),
    });
}
