namespace FullstackTemplate.IntegrationTests.Resources;

using System.Net;
using System.Text;
using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.Server.Resources.Entrypoints.Mcp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

/// <summary>
/// The ID token of the BFF can have the API audience, and the browser sees it in the id_token_hint of the logout.
/// The real JWT bearer setup of the Server must take only access tokens.
/// </summary>
public class AccessTokenTypeTests : TestBase
{
    private const string Authority = "https://idp.test/realms/app";
    private const string Audience = "test-api";
    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("a-test-signing-key-of-at-least-32-bytes!"));

    public static IEnumerable<(string, string, object)> TokensThatAreNotAccessTokens =>
    [
        ("Keycloak ID token", "typ", "ID"),
        ("Keycloak logout token", "typ", "Logout"),
        ("Keycloak refresh token", "typ", "Refresh"),
        ("FusionAuth ID token", "tty", "idt"),
        ("Cognito ID token", "token_use", "id"),
        ("Pocket ID ID token", "type", "id-token"),
        ("Back-channel logout token", "events", new Dictionary<string, object> { ["http://schemas.openid.net/event/backchannel-logout"] = new Dictionary<string, object>() }),
    ];

    public static IEnumerable<(string, string, object)> AccessTokens =>
    [
        ("Keycloak access token", "typ", "Bearer"),
        ("FusionAuth access token", "tty", "at"),
        ("Cognito access token", "token_use", "access"),
        // A Pocket ID access token has no type claim. It has client_id, as RFC 9068 gives.
        ("Pocket ID access token", "client_id", "aspire-app"),
    ];

    [Test]
    [MethodDataSource(nameof(TokensThatAreNotAccessTokens))]
    public async Task a_token_that_is_not_an_access_token_gets_401(string @case, string claim, object value)
    {
        // Arrange
        await using var app = await StartAsync();

        // Act
        using var response = await GetAsync(app, Token(claim, value));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, @case);
    }

    [Test]
    [MethodDataSource(nameof(AccessTokens))]
    public async Task an_access_token_gets_200(string @case, string claim, object value)
    {
        // Arrange
        await using var app = await StartAsync();

        // Act
        using var response = await GetAsync(app, Token(claim, value));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, @case);
    }

    private static async Task<HttpResponseMessage> GetAsync(WebApplication app, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/probe");
        request.Headers.Authorization = new("Bearer", token);
        return await app.GetTestClient().SendAsync(request);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["Auth:Authority"] = Authority;
        builder.Configuration["Auth:Audience"] = Audience;
        builder.Configuration[McpResourceMetadata.PublicOriginKey] = "https://server.test";
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddJwtBearerAuthentication(builder.Configuration, builder.Environment);
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                new OpenIdConnectConfiguration { Issuer = Authority, SigningKeys = { SigningKey } }));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/{**path}", () => Results.Ok()).RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private static string Token(string claim, object value) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = Authority,
        Audience = Audience,
        Expires = DateTime.UtcNow.AddMinutes(5),
        Claims = new Dictionary<string, object> { ["sub"] = "user-1", ["azp"] = "bff", [claim] = value },
        SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
    });
}
