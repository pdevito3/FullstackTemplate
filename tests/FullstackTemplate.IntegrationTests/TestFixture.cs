namespace FullstackTemplate.IntegrationTests;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Resources.Extensions;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Testcontainers.PostgreSql;

[CollectionDefinition(nameof(TestFixture))]
public class TestFixtureCollection : ICollectionFixture<TestFixture> { }

public class TestFixture : IAsyncLifetime
{
    public static IServiceScopeFactory BaseScopeFactory = null!;
    private PostgreSqlContainer _dbContainer = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = TestingConsts.IntegrationTestingEnvName
        });

        // Start PostgreSQL container
        _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _dbContainer.StartAsync();
        
        builder.Configuration[$"ConnectionStrings:{DatabaseConsts.DatabaseName}"] = _dbContainer.GetConnectionString();

        // Configure auth for testing (these values are not used since auth is mocked)
        builder.Configuration["Auth:Authority"] = "https://localhost";
        builder.Configuration["Auth:Audience"] = "test-api";

        // Configure services
        builder.ConfigureServices();

        var services = builder.Services;

        // Mock ICurrentUserService for simplified user management in tests
        services.ReplaceServiceWithSingletonMock<ICurrentUserService>();

        var provider = services.BuildServiceProvider();
        BaseScopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        // Run migrations
        await using var scope = BaseScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ReplaceServiceWithSingletonMock<TService>(this IServiceCollection services)
        where TService : class
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(_ => Substitute.For<TService>());
        return services;
    }
}
