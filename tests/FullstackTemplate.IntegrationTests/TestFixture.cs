namespace FullstackTemplate.IntegrationTests;

using AutoBogus;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources.Extensions;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public class TestFixture : IAsyncLifetime
{
    /// <summary>
    /// The one machine client that the test configuration lets see and manage all tenants.
    /// </summary>
    public const string TrustedMachineClientId = "trusted-machine";

    public static IServiceScopeFactory BaseScopeFactory = null!;
    public static TestSeed Seed = null!;
    public static Tenant DefaultTenant => Seed.Tenant;
    public static User DefaultUser => Seed.User;
    private readonly PostgresTestDatabase _database = new();

    public async ValueTask InitializeAsync()
    {
        ConfigureAutoFaker();

        // Tests never reload configuration. A file watcher on appsettings can hang on macOS when the system is busy.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = TestingConsts.IntegrationTestingEnvName
        });

        await _database.StartAsync();
        builder.Configuration[$"ConnectionStrings:{DatabaseConsts.DatabaseName}"] = _database.ConnectionString;

        // Configure auth for testing (these values are not used since auth is mocked)
        builder.Configuration["Auth:Authority"] = "https://localhost";
        builder.Configuration["Auth:Audience"] = "test-api";
        builder.Configuration[$"{TenancyOptions.SectionName}:{nameof(TenancyOptions.AllTenantClientIds)}:0"] = TrustedMachineClientId;

        // Configure services
        builder.ConfigureServices();

        var services = builder.Services;

        // Mock ICurrentUserService for simplified user management in tests.
        // The mock is scoped, so each TestingServiceScope has its own current user.
        // This lets tests run in parallel.
        services.ReplaceServiceWithScopedMock<ICurrentUserService>();

        var provider = services.BuildServiceProvider();
        BaseScopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        // Run migrations and seed in a system scope. With no opt-in, a scope with no HTTP request sees no tenant.
        await using var scope = BaseScopeFactory.CreateSystemScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();

        // Create the seed once for all tests. The seed user is the current user,
        // so ITenantIdProvider resolves the seed tenant.
        Seed = TestSeed.Generate();
        dbContext.Tenants.Add(Seed.Tenant);
        dbContext.Users.Add(Seed.User);
        await dbContext.SaveChangesAsync();

        // Set TestContext so the DTO fakers use the seed tenant by default
        TestContext.DefaultTenantId = Seed.Tenant.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private static void ConfigureAutoFaker()
    {
        AutoFaker.Configure(builder =>
        {
            builder.WithDateTimeKind(DateTimeKind.Utc)
                .WithRecursiveDepth(3)
                .WithTreeDepth(1)
                .WithRepeatCount(1);
        });
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ReplaceServiceWithScopedMock<TService>(this IServiceCollection services)
        where TService : class
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddScoped(_ => Substitute.For<TService>());
        return services;
    }
}
