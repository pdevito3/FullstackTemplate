namespace FullstackTemplate.IntegrationTests;

// One TestFixture (one database and one service provider) for the whole assembly.
// TUnit runs all tests in parallel by default, so each test must create its own data
// and its own TestingServiceScope.
public static class AssemblyHooks
{
    private static TestFixture? _fixture;

    [Before(Assembly)]
    public static async Task StartAsync()
    {
        _fixture = new TestFixture();
        await _fixture.InitializeAsync();
    }

    [After(Assembly)]
    public static async Task StopAsync()
    {
        if (_fixture is not null)
            await _fixture.DisposeAsync();
    }
}
