namespace FullstackTemplate.FunctionalTests;

using System.Diagnostics;
using AutoBogus;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using WebMotions.Fake.Authentication.JwtBearer;

/// <summary>
/// One app and one database for the whole test assembly. Starting them once per test made each
/// test pay for a new container and a new migration run.
/// </summary>
public class TestingWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    /// <summary>
    /// The one machine client that the test server lets read the rows of all tenants.
    /// </summary>
    public const string TrustedMachineClientId = "trusted-machine";

    /// <summary>
    /// The running factory for the assembly. Set when the fixture starts.
    /// </summary>
    public static TestingWebApplicationFactory Instance { get; private set; } = null!;

    /// <summary>
    /// Every span the server exports, in stop order. Read it through <see cref="TestBase.ExportedSpans"/>.
    /// </summary>
    public List<Activity> ExportedActivities { get; } = [];

    /// <summary>
    /// Every metric snapshot the server exports. Call <c>MeterProvider.ForceFlush</c> to export now.
    /// </summary>
    public List<MetricSnapshot> ExportedMetrics { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestingConsts.FunctionalTestingEnvName);

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

            // Capture the exported spans so tests can make assertions on the real trace pipeline
            services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddInMemoryExporter(ExportedActivities))
                .WithMetrics(metrics => metrics.AddInMemoryExporter(ExportedMetrics));
        });
    }

    public async ValueTask InitializeAsync()
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
        Environment.SetEnvironmentVariable("Tenancy__AllTenantClientIds__0", TrustedMachineClientId);

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

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
