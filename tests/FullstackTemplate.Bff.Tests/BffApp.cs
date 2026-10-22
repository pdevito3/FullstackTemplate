namespace FullstackTemplate.Bff.Tests;

using System.Net;
using System.Web;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Core;

/// <summary>
/// The BFF on a test server, with the <see cref="FakeIdentityProvider"/> as its identity provider and the
/// <see cref="EchoApi"/> as its server API. One instance serves the whole assembly. Each test gets its own
/// client, and thus its own cookies.
/// </summary>
public sealed class BffApp : WebApplicationFactory<Program>
{
    /// <summary>
    /// The origin of the test server. It is https, so that the browser rules for Secure cookies apply.
    /// </summary>
    public static readonly Uri Origin = new("https://localhost");

    public static BffApp Instance { get; private set; } = null!;

    public FakeIdentityProvider IdentityProvider { get; private set; } = null!;

    public EchoApi Api { get; private set; } = null!;

    /// <summary>
    /// Each log event of the BFF. Filter by a value that is unique to the test.
    /// </summary>
    public CapturedLogs Logs { get; } = new();

    public static async Task StartAsync()
    {
        var identityProvider = await FakeIdentityProvider.StartAsync();
        var api = await EchoApi.StartAsync();

        // Program.cs reads these settings before WebApplicationFactory can add its own configuration.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        Environment.SetEnvironmentVariable("Auth__Authority", identityProvider.Issuer);
        Environment.SetEnvironmentVariable("Auth__ClientId", FakeIdentityProvider.ClientId);
        Environment.SetEnvironmentVariable("Auth__ClientSecret", FakeIdentityProvider.ClientSecret);
        Environment.SetEnvironmentVariable("Auth__Audience", "test-api");
        Environment.SetEnvironmentVariable("Auth__RequireHttpsMetadata", "false");
        Environment.SetEnvironmentVariable("services__server__http__0", api.Url);
        Environment.SetEnvironmentVariable("services__webfrontend__http__0", "http://localhost:5173");

        var app = new BffApp { IdentityProvider = identityProvider, Api = api };
        // Start the host now, so that a startup failure fails the assembly hook with its own message
        _ = app.Server;
        Instance = app;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await IdentityProvider.DisposeAsync();
        await Api.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            // Serilog reads the sinks from the services (ReadFrom.Services)
            services.AddSingleton<ILogEventSink>(Logs);
            services.AddTestAuthorization();
        });
    }

    /// <summary>
    /// A client with its own cookie container, that does not follow redirects.
    /// </summary>
    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = Origin,
        HandleCookies = true,
    });

    /// <summary>
    /// Runs the full login: <c>/bff/login</c>, the authorize endpoint of the fake provider, and the
    /// <c>/signin-oidc</c> callback. <paramref name="providerQuery"/> holds the test parameters of
    /// <see cref="FakeIdentityProvider"/>, for example <c>test_sid=s1</c>.
    /// </summary>
    public static async Task<HttpResponseMessage> SignInAsync(HttpClient browser, string loginQuery = "", string providerQuery = "")
    {
        var login = await browser.GetAsync($"/bff/login{loginQuery}");
        if (login.StatusCode != HttpStatusCode.Redirect)
            throw new InvalidOperationException($"/bff/login returned {(int)login.StatusCode}, not 302.");

        var callback = await AuthorizeAsync(login.Headers.Location!, providerQuery);
        return await browser.GetAsync(callback.PathAndQuery);
    }

    /// <summary>
    /// Sends the authorize request of a login to the fake provider and returns the callback URL that it gives.
    /// </summary>
    public static async Task<Uri> AuthorizeAsync(Uri authorizeUrl, string providerQuery = "")
    {
        using var provider = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var url = string.IsNullOrEmpty(providerQuery) ? authorizeUrl.ToString() : $"{authorizeUrl}&{providerQuery}";
        var response = await provider.GetAsync(new Uri(url));
        return response.Headers.Location
            ?? throw new InvalidOperationException($"The authorize endpoint returned {(int)response.StatusCode} with no Location.");
    }

    /// <summary>
    /// The query parameters of a URL.
    /// </summary>
    public static System.Collections.Specialized.NameValueCollection Query(Uri url) => HttpUtility.ParseQueryString(url.Query);
}

public static class AssemblyHooks
{
    [Before(Assembly)]
    public static Task StartAsync() => BffApp.StartAsync();

    [After(Assembly)]
    public static async Task StopAsync()
    {
        if (BffApp.Instance is not null)
            await BffApp.Instance.DisposeAsync();
    }
}
