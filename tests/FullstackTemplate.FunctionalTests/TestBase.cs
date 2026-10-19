namespace FullstackTemplate.FunctionalTests;

using System.Diagnostics;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

// All tests run in parallel on the one factory of the assembly (see AssemblyHooks.cs).
public class TestBase : IDisposable
{
    private static TestingWebApplicationFactory Factory => TestingWebApplicationFactory.Instance;
    private static IServiceScopeFactory ScopeFactory => Factory.Services.GetRequiredService<IServiceScopeFactory>();

    private readonly int _firstSpanIndex;

    /// <summary>
    /// A new client for each test, so a header that one test sets (for example a bearer token)
    /// does not leak into the next test.
    /// </summary>
    protected HttpClient FactoryClient { get; }

    public TestBase()
    {
        FactoryClient = Factory.CreateClient();
        lock (Factory.ExportedActivities)
            _firstSpanIndex = Factory.ExportedActivities.Count;
    }

    public void Dispose()
    {
        FactoryClient.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A snapshot of the spans that the server exported since this test started.
    /// </summary>
    protected IReadOnlyList<Activity> ExportedSpans()
    {
        lock (Factory.ExportedActivities)
            return Factory.ExportedActivities.Skip(_firstSpanIndex).ToList();
    }

    /// <summary>
    /// A snapshot of every span that the server exported, including the spans from startup.
    /// </summary>
    protected static IReadOnlyList<Activity> AllExportedSpans()
    {
        lock (Factory.ExportedActivities)
            return Factory.ExportedActivities.ToList();
    }

    // The helpers below arrange and check data for the test, outside any HTTP request. They are system
    // work, so each one opts in to all tenants with a system scope.

    public static async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = ScopeFactory.CreateSystemScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        return await mediator.Send(request);
    }

    public static async Task<TEntity?> FindAsync<TEntity>(params object[] keyValues)
        where TEntity : class
    {
        await using var scope = ScopeFactory.CreateSystemScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.FindAsync<TEntity>(keyValues);
    }

    public static async Task AddAsync<TEntity>(TEntity entity) where TEntity : class
    {
        await using var scope = ScopeFactory.CreateSystemScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Add(entity);
        await context.SaveChangesAsync();
    }

    public static async Task ExecuteScopeAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = ScopeFactory.CreateSystemScope();
        await action(scope.ServiceProvider);
    }

    public static async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = ScopeFactory.CreateSystemScope();
        return await action(scope.ServiceProvider);
    }

    public static Task ExecuteDbContextAsync(Func<AppDbContext, Task> action)
        => ExecuteScopeAsync(sp => action(sp.GetRequiredService<AppDbContext>()));

    public static Task<T> ExecuteDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
        => ExecuteScopeAsync(sp => action(sp.GetRequiredService<AppDbContext>()));

    /// <summary>
    /// Starts a scenario on an in-memory seed. The functional tests have no seed rows in the database,
    /// so start the scenario with <c>AddTenant</c>. <c>BuildAsync</c> saves the new entities in one save.
    /// </summary>
    protected static GraphBuilder<TestSeed, Tenant[]> StartGraph()
        => TestGraph.Start(TestSeed.Generate(), new ScopedDbContextStore());

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

    /// <summary>Saves a graph through a new service scope, the same as a separate request.</summary>
    private sealed class ScopedDbContextStore : IGraphStore
    {
        public async ValueTask SaveAsync(IReadOnlyList<object> entities, CancellationToken cancellationToken)
            => await ExecuteDbContextAsync(async db => await db.AsGraphStore().SaveAsync(entities, cancellationToken));
    }
}
