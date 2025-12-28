namespace FullstackTemplate.FunctionalTests;

using System.Net.Http.Json;
using AutoBogus;
using FullstackTemplate.Server.Databases;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

[Collection(nameof(TestBase))]
public class TestBase : IDisposable
{
    private readonly TestingWebApplicationFactory _factory;
    private static IServiceScopeFactory _scopeFactory = null!;
    protected static HttpClient FactoryClient { get; private set; } = null!;

    public TestBase()
    {
        _factory = new TestingWebApplicationFactory();
        _factory.InitializeAsync().GetAwaiter().GetResult();

        _scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
        FactoryClient = _factory.CreateClient(new WebApplicationFactoryClientOptions());

        AutoFaker.Configure(builder =>
        {
            builder.WithDateTimeKind(DateTimeKind.Utc)
                .WithRecursiveDepth(3)
                .WithTreeDepth(1)
                .WithRepeatCount(1);
        });
    }

    public void Dispose()
    {
        FactoryClient.Dispose();
        _factory.DisposeAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    public static async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        return await mediator.Send(request);
    }

    public static async Task<TEntity?> FindAsync<TEntity>(params object[] keyValues)
        where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.FindAsync<TEntity>(keyValues);
    }

    public static async Task AddAsync<TEntity>(TEntity entity) where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Add(entity);
        await context.SaveChangesAsync();
    }

    public static async Task ExecuteScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = _scopeFactory.CreateScope();
        await action(scope.ServiceProvider);
    }

    public static async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public static Task ExecuteDbContextAsync(Func<AppDbContext, Task> action)
        => ExecuteScopeAsync(sp => action(sp.GetRequiredService<AppDbContext>()));

    public static Task<T> ExecuteDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
        => ExecuteScopeAsync(sp => action(sp.GetRequiredService<AppDbContext>()));

    public static async Task<int> InsertAsync<T>(params T[] entities) where T : class
    {
        return await ExecuteDbContextAsync(async db =>
        {
            foreach (var entity in entities)
            {
                db.Set<T>().Add(entity);
            }
            return await db.SaveChangesAsync();
        });
    }
}
