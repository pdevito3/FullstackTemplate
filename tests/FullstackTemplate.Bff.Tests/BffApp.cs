namespace FullstackTemplate.Bff.Tests;

using System.Net;
using System.Runtime.CompilerServices;
using System.Web;
using FullstackTemplate.Bff.Sessions;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.AspNetCore;
using Serilog.Core;

/// <summary>
/// The BFF on a test server, with the <see cref="FakeIdentityProvider"/> as its identity provider, the
/// <see cref="EchoApi"/> as its server API, and a Postgres container for <c>bffdb</c>. One instance serves the
/// whole assembly. Each test gets its own client, and thus its own cookies. A test that needs other settings, or
/// a second BFF instance on the same <c>bffdb</c>, starts its own with <see cref="StartAnotherInstance"/>.
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
    /// <remarks>
    /// Each host replaces the static Serilog logger when it starts, and Program.cs closes it when the host stops, at
    /// a time that the tests do not control. So the static loggers of the BFF use the logger of the first host, and
    /// the request log of each host uses the logger of that host. All instances share one sink, so that the events
    /// of each instance arrive here.
    /// </remarks>
    public static CapturedLogs Logs { get; } = new();

    public static PostgresTestDatabase Database { get; private set; } = null!;

    // Program.cs assigns a new bootstrap logger to the static Log.Logger and the host freezes it. Two hosts that
    // start at the same time can freeze the same logger, which fails the second host.
    private static readonly Lock StartLock = new();

    // WebApplicationFactory adds each client to a List, which is not safe for parallel tests. A lost add leaves a
    // null in the list, and DisposeAsync then throws.
    private readonly Lock _clientLock = new();

    private IReadOnlyDictionary<string, string?> _settings = new Dictionary<string, string?>();
    private Action<IServiceCollection>? _services;
    private bool _ownsDependencies;

    public static async Task StartAsync()
    {
        var database = new PostgresTestDatabase();
        await database.StartAsync();
        Database = database;
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
        Environment.SetEnvironmentVariable($"ConnectionStrings__{BffDbContext.DatabaseName}", database.ConnectionString);

        var app = new BffApp { IdentityProvider = identityProvider, Api = api, _ownsDependencies = true };
        // Start the host now, so that a startup failure fails the assembly hook with its own message
        app.StartHost();
        BindStaticLoggers();
        Instance = app;
    }

    /// <summary>
    /// Starts one more BFF on the same API and <c>bffdb</c>, with the extra <paramref name="settings"/>, and the
    /// changes of <paramref name="services"/> after the services of the app. It uses the same identity provider, or
    /// <paramref name="identityProvider"/>. The caller disposes it.
    /// </summary>
    public static BffApp StartAnotherInstance(
        IReadOnlyDictionary<string, string?>? settings = null,
        Action<IServiceCollection>? services = null,
        FakeIdentityProvider? identityProvider = null)
    {
        var app = new BffApp
        {
            IdentityProvider = identityProvider ?? Instance.IdentityProvider,
            Api = Instance.Api,
            _settings = settings ?? new Dictionary<string, string?>(),
            _services = services,
        };
        try
        {
            app.StartHost();
        }
        catch
        {
            // A test of a startup failure gets no instance to dispose, so dispose the factory and its host here
            app.Dispose();
            throw;
        }

        return app;
    }

    private void StartHost()
    {
        lock (StartLock)
            _ = Server;
    }

    /// <summary>
    /// Runs the type initializer of each BFF type that has a static Serilog logger, while the first host is the only
    /// host. A <c>Log.ForContext</c> logger keeps the static logger of the moment it is made, so a first use after
    /// another host stops would keep a closed, silent logger for the rest of the run.
    /// </summary>
    private static void BindStaticLoggers()
    {
        const System.Reflection.BindingFlags statics =
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        foreach (var type in typeof(Program).Assembly.GetTypes())
        {
            if (!type.ContainsGenericParameters && type.GetFields(statics).Any(f => f.FieldType == typeof(Serilog.ILogger)))
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (!_ownsDependencies)
            return;

        await IdentityProvider.DisposeAsync();
        await Api.DisposeAsync();
        await Database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(_settings));
        // AddBffAuthentication reads Auth:Authority before this configuration exists, so set the authority on the
        // options. A Configure runs before the post-configure step that creates the ConfigurationManager.
        builder.ConfigureTestServices(services => services.Configure<OpenIdConnectOptions>(
            OpenIdConnectDefaults.AuthenticationScheme, options => options.Authority = IdentityProvider.Issuer));
        builder.ConfigureTestServices(services =>
        {
            // Serilog reads the sinks from the services (ReadFrom.Services)
            services.AddSingleton<ILogEventSink>(Logs);
            // Without a logger, the request log uses the static logger at each request, which another host can have
            // closed
            services.AddOptions<RequestLoggingOptions>()
                .Configure<Serilog.ILogger>((options, logger) => options.Logger = logger);
            services.AddTestAuthorization();
        });
        if (_services is not null)
            builder.ConfigureTestServices(_services);
    }

    /// <summary>
    /// Runs <paramref name="action"/> with the BFF session store of this BFF, in its own scope.
    /// </summary>
    public async Task<T> WithStoreAsync<T>(Func<IBffSessionStore, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IBffSessionStore>());
    }

    /// <summary>
    /// The BFF session key in the last <c>__Host-bff</c> cookie that <paramref name="response"/> sets, or null if
    /// it sets none or only deletes it.
    /// </summary>
    public string? SessionKeyIn(HttpResponseMessage response)
    {
        var value = SessionCookieValue(response);
        if (string.IsNullOrEmpty(value))
            return null;

        var cookieOptions = Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        return cookieOptions.TicketDataFormat.Unprotect(value)?.Principal.FindFirst(SessionKey.ClaimType)?.Value;
    }

    /// <summary>
    /// The value of the last <c>__Host-bff</c> cookie that <paramref name="response"/> sets.
    /// </summary>
    public static string? SessionCookieValue(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Where(c => c.StartsWith("__Host-bff=", StringComparison.Ordinal))
                .Select(c => c["__Host-bff=".Length..].Split(';')[0])
                .LastOrDefault()
            : null;

    /// <summary>
    /// A client with its own cookie container, that does not follow redirects.
    /// </summary>
    public HttpClient CreateBrowser()
    {
        lock (_clientLock)
        {
            return CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = Origin,
                HandleCookies = true,
            });
        }
    }

    /// <summary>
    /// A client on the cookies of <paramref name="cookies"/>, that does not follow redirects. Two clients of two
    /// BFF instances with the same container act as one browser.
    /// </summary>
    public HttpClient CreateBrowser(CookieContainer cookies)
    {
        lock (_clientLock)
            return CreateDefaultClient(Origin, new Microsoft.AspNetCore.Mvc.Testing.Handlers.CookieContainerHandler(cookies));
    }

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
