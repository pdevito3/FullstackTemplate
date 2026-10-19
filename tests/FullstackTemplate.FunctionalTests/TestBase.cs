namespace FullstackTemplate.FunctionalTests;

// No [Collection] attribute: all tests run in parallel on the assembly fixture
// (see AssemblyFixtureRegistration.cs).
public class TestBase : IDisposable
{
    private static TestingWebApplicationFactory Factory => TestingWebApplicationFactory.Instance;

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
}
