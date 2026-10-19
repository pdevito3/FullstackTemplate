namespace FullstackTemplate.FunctionalTests;

// One TestingWebApplicationFactory (one database and one test server) for the whole assembly.
// TUnit runs all tests in parallel by default, so each test must create its own data.
public static class AssemblyHooks
{
    private static TestingWebApplicationFactory? _factory;

    [Before(Assembly)]
    public static async Task StartAsync()
    {
        _factory = new TestingWebApplicationFactory();
        await _factory.InitializeAsync();
    }

    [After(Assembly)]
    public static async Task StopAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync();
    }
}
