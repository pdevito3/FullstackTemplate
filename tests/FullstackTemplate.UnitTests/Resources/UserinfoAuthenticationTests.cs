namespace FullstackTemplate.UnitTests.Resources;

using System.Net;
using System.Security.Claims;
using System.Text;
using FullstackTemplate.Server.Resources.Entrypoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;

/// <summary>
/// Auth:AccessTokenValidation=Userinfo, for the opaque access tokens of the OpenID conformance suite.
/// </summary>
public class UserinfoAuthenticationTests
{
    private const string Authority = "https://idp.test/test/a/alias/";
    private const string AppClientId = "app";

    [Test]
    public void userinfo_validation_outside_development_fails_at_startup()
    {
        var exception = Should.Throw<InvalidOperationException>(() => BuildServices(Environments.Production));

        exception.Message.ShouldContain(UserinfoAuthenticationHandler.AccessTokenValidationKey);
    }

    [Test]
    public async Task a_token_that_the_userinfo_endpoint_accepts_authenticates_with_the_userinfo_claims()
    {
        var result = await AuthenticateAsync("Bearer good-token");

        result.Succeeded.ShouldBeTrue();
        result.Principal!.Identity!.Name.ShouldBe("Ada");
        result.Principal.FindFirstValue("sub").ShouldBe("user-1");
    }

    [Test]
    public async Task a_userinfo_response_with_no_client_claim_gets_the_client_of_the_app()
    {
        var result = await AuthenticateAsync("Bearer good-token");

        result.Principal.ShouldNotBeNull().FindFirstValue("client_id").ShouldBe(AppClientId);
    }

    [Test]
    public async Task a_userinfo_response_with_a_client_claim_keeps_it()
    {
        var result = await AuthenticateAsync("Bearer agent-token");

        var principal = result.Principal.ShouldNotBeNull();
        principal.FindFirstValue("azp").ShouldBe("other-client");
        principal.FindFirstValue("client_id").ShouldBeNull();
    }

    [Test]
    public async Task a_token_that_the_userinfo_endpoint_rejects_fails()
    {
        var result = await AuthenticateAsync("Bearer other-token");

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
    }

    [Test]
    public async Task a_request_with_no_bearer_token_has_no_result()
    {
        var result = await AuthenticateAsync(null);

        result.None.ShouldBeTrue();
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(string? authorization)
    {
        await using var services = BuildServices(Environments.Development);
        var context = new DefaultHttpContext { RequestServices = services };
        if (authorization is not null)
            context.Request.Headers.Authorization = authorization;

        return await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
    }

    private static ServiceProvider BuildServices(string environmentName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = Authority,
                ["Auth:Audience"] = "client",
                ["Auth:ClientId"] = AppClientId,
                [UserinfoAuthenticationHandler.AccessTokenValidationKey] = "Userinfo",
            })
            .Build();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJwtBearerAuthentication(configuration, environment);
        services.AddHttpClient(UserinfoAuthenticationHandler.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new FakeIdentityProvider());
        return services.BuildServiceProvider();
    }

    private sealed class FakeIdentityProvider : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = request.RequestUri!.AbsoluteUri switch
            {
                $"{Authority}.well-known/openid-configuration" => Json("""{"userinfo_endpoint":"https://idp.test/test/a/alias/userinfo"}"""),
                $"{Authority}userinfo" when request.Headers.Authorization?.Parameter == "good-token" =>
                    Json("""{"sub":"user-1","name":"Ada","email_verified":true}"""),
                $"{Authority}userinfo" when request.Headers.Authorization?.Parameter == "agent-token" =>
                    Json("""{"sub":"user-1","azp":"other-client"}"""),
                _ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            };
            return Task.FromResult(response);
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
