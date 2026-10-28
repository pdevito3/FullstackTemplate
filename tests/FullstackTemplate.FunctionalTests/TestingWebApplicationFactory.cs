namespace FullstackTemplate.FunctionalTests;

using System.Diagnostics;
using AutoBogus;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants.DomainEvents;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog.Core;
using TestUtilities;
using WebMotions.Fake.Authentication.JwtBearer;

/// <summary>
/// One app and one database for the whole test assembly. Starting them once per test made each
/// test pay for a new container and a new migration run.
/// </summary>
public sealed class TestingWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PostgresTestDatabase _database = new();

    // WebApplicationFactory adds each client to a List with no lock, and the tests create their clients in parallel.
    // Two adds at the same time can lose a client or leave a null in the list, and DisposeAsync then throws.
    private readonly Lock _clientLock = new();

    /// <summary>
    /// The machine client that the test server lets manage all tenants. It also reads the rows of all tenants.
    /// </summary>
    public const string TrustedMachineClientId = "trusted-machine";

    /// <summary>
    /// The machine client that the test server lets read the rows of all tenants, with no right to change them.
    /// </summary>
    public const string ReadOnlyMachineClientId = "read-only-machine";

    /// <summary>
    /// The client ID of the app (the BFF). A user token from this client acts as its user.
    /// </summary>
    public const string FirstPartyClientId = "test-app";

    /// <summary>
    /// The origin at which MCP clients reach the test server. The MCP resource metadata names it.
    /// </summary>
    public const string McpPublicOrigin = "https://server.test";

    /// <summary>
    /// The running factory for the assembly. Set when the fixture starts.
    /// </summary>
    public static TestingWebApplicationFactory Instance { get; private set; } = null!;

    /// <summary>
    /// Every span the server exports, in stop order. Read it through <see cref="TestBase.ExportedSpans"/>.
    /// </summary>
    public ExportedTelemetry<Activity> ExportedActivities { get; } = new();

    /// <summary>
    /// Every metric snapshot the server exports. Call <c>MeterProvider.ForceFlush</c> to export now.
    /// </summary>
    public ExportedTelemetry<MetricSnapshot> ExportedMetrics { get; } = new();

    public CapturedLogs CapturedLogs { get; } = new();

    public FailingSaves FailingSaves { get; } = new();

    public StallingHealthCheck StallingHealthCheck { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestingConsts.FunctionalTestingEnvName);
        // A health probe gets a Verbose request log, and a test reads it
        builder.UseSetting("Serilog:MinimumLevel:Override:Serilog.AspNetCore.RequestLoggingMiddleware", "Verbose");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });

        builder.ConfigureServices(services =>
        {
            // Replace authentication with fake JWT bearer
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = FakeJwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = FakeJwtBearerDefaults.AuthenticationScheme;
            }).AddFakeJwtBearer();

            // Add migration hosted service
            services.AddHostedService<MigrationHostedService<AppDbContext>>();

            services.AddScoped<IDomainEventHandler<TenantUpdated>>(_ => new NoOpTenantUpdatedHandler());

            // Capture the exported spans so tests can make assertions on the real trace pipeline
            services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddInMemoryExporter(ExportedActivities.ExporterTarget))
                .WithMetrics(metrics => metrics.AddInMemoryExporter(ExportedMetrics.ExporterTarget));
            services.AddSingleton<ILogEventSink>(CapturedLogs);
            services.ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(FailingSaves));
            services.AddHealthChecks().AddCheck("stalling", StallingHealthCheck);
        });
    }

    public async Task InitializeAsync()
    {
        // Tests never reload configuration. A file watcher on appsettings can hang on macOS when the system is busy.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");

        await _database.StartAsync();

        Environment.SetEnvironmentVariable(
            $"ConnectionStrings__{DatabaseConsts.DatabaseName}",
            _database.ConnectionString);

        // Auth configuration required by AddJwtBearerAuthentication
        Environment.SetEnvironmentVariable("Auth__Authority", "https://localhost");
        Environment.SetEnvironmentVariable("Auth__Audience", "test-api");
        Environment.SetEnvironmentVariable("Auth__ClientId", FirstPartyClientId);
        Environment.SetEnvironmentVariable("Mcp__PublicOrigin", McpPublicOrigin);
        Environment.SetEnvironmentVariable("Tenancy__ManageAllTenantsClientIds__0", TrustedMachineClientId);
        Environment.SetEnvironmentVariable("Tenancy__ReadAllTenantsClientIds__0", ReadOnlyMachineClientId);

        // Aspire sets this on each project in development, so the request spans keep the query as the browser sent it
        Environment.SetEnvironmentVariable("OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_DISABLE_URL_QUERY_REDACTION", "true");

        // Disable Aspire health checks that require database connectivity during startup
        Environment.SetEnvironmentVariable("Aspire__Npgsql__EntityFrameworkCore__PostgreSQL__DisableHealthChecks", "true");

        AutoFaker.Configure(builder =>
        {
            builder.WithDateTimeKind(DateTimeKind.Utc)
                .WithRecursiveDepth(3)
                .WithTreeDepth(1)
                .WithRepeatCount(1);
        });

        // Start the host now, so startup work such as migrations is done before the first test
        _ = Services;
        Instance = this;
    }

    public new HttpClient CreateClient()
    {
        lock (_clientLock)
            return base.CreateClient();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}

public sealed class NoOpTenantUpdatedHandler : IDomainEventHandler<TenantUpdated>
{
    public Task Handle(TenantUpdated domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
