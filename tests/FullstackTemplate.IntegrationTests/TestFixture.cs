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

public sealed class TestFixture : IAsyncDisposable
{
    /// <summary>
    /// The one machine client that the test configuration lets see and manage all tenants.
    /// </summary>
    public const string TrustedMachineClientId = "trusted-machine";

    /// <summary>
    /// The one machine client that the test configuration lets see all tenants, but not manage them.
    /// </summary>
    public const string ReadOnlyMachineClientId = "read-only-machine";

    public static IServiceScopeFactory BaseScopeFactory { get; private set; } = null!;
    private static string _connectionString = null!;
    public static TestSeed Seed { get; private set; } = null!;
    public static Tenant DefaultTenant => Seed.Tenant;
    public static User DefaultUser => Seed.User;
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        ConfigureAutoFaker();

        // Tests never reload configuration. A file watcher on appsettings can hang on macOS when the system is busy.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");

        await _database.StartAsync();
        _connectionString = _database.ConnectionString;

        var provider = AddServices().BuildServiceProvider();
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

    public static ServiceProvider BuildServiceProvider(Action<IServiceCollection> addTestServices)
    {
        var services = AddServices();
        addTestServices(services);
        return services.BuildServiceProvider();
    }

    private static IServiceCollection AddServices()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = TestingConsts.IntegrationTestingEnvName
        });
        builder.Configuration[$"ConnectionStrings:{DatabaseConsts.DatabaseName}"] = _connectionString;

        builder.Configuration["Auth:Authority"] = "https://localhost";
        builder.Configuration["Auth:Audience"] = "test-api";
        builder.Configuration[$"{TenancyOptions.SectionName}:{nameof(TenancyOptions.ManageAllTenantsClientIds)}:0"] = TrustedMachineClientId;
        builder.Configuration[$"{TenancyOptions.SectionName}:{nameof(TenancyOptions.ReadAllTenantsClientIds)}:0"] = ReadOnlyMachineClientId;

        builder.ConfigureServices();

        builder.Services.ReplaceServiceWithScopedMock<ICurrentUserService>();

        return builder.Services;
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
