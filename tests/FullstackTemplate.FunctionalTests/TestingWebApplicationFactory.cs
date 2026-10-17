namespace FullstackTemplate.FunctionalTests;

using System.Diagnostics;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Testcontainers.PostgreSql;
using WebMotions.Fake.Authentication.JwtBearer;

[CollectionDefinition(nameof(TestBase))]
public class TestingWebApplicationFactoryCollection : ICollectionFixture<TestingWebApplicationFactory> { }

public class TestingWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer _dbContainer = null!;

    /// <summary>
    /// Every span the server exports, in stop order. Read it through <see cref="TestBase.ExportedSpans"/>.
    /// </summary>
    public List<Activity> ExportedActivities { get; } = [];

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
            services.AddOpenTelemetry().WithTracing(tracing => tracing.AddInMemoryExporter(ExportedActivities));
        });
    }

    public async Task InitializeAsync()
    {
        _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _dbContainer.StartAsync();

        Environment.SetEnvironmentVariable(
            $"ConnectionStrings__{DatabaseConsts.DatabaseName}",
            _dbContainer.GetConnectionString());

        // Auth configuration required by AddJwtBearerAuthentication
        Environment.SetEnvironmentVariable("Auth__Authority", "https://localhost");
        Environment.SetEnvironmentVariable("Auth__Audience", "test-api");

        // Disable Aspire health checks that require database connectivity during startup
        Environment.SetEnvironmentVariable("Aspire__Npgsql__EntityFrameworkCore__PostgreSQL__DisableHealthChecks", "true");
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }
}
