namespace FullstackTemplate.FunctionalTests;

using Microsoft.Extensions.DependencyInjection;

// No [Collection] attribute: all tests run in parallel on the assembly fixture
// (see AssemblyFixtureRegistration.cs).
public class TestBase : IDisposable
{
    private static TestingWebApplicationFactory Factory => TestingWebApplicationFactory.Instance;
    private static IServiceScopeFactory ScopeFactory => Factory.Services.GetRequiredService<IServiceScopeFactory>();

    /// <summary>
    /// A new client for each test, so a header that one test sets (for example a bearer token)
    /// does not leak into the next test.
    /// </summary>
    protected HttpClient FactoryClient { get; }

    public TestBase()
    {
        FactoryClient = Factory.CreateClient();
    }

    public void Dispose()
    {
        FactoryClient.Dispose();
        GC.SuppressFinalize(this);
    }

    public static async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = ScopeFactory.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }
}
