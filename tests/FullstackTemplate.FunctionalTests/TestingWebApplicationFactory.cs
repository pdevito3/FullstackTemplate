namespace FullstackTemplate.FunctionalTests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

/// <summary>
/// One app for the whole test assembly.
/// </summary>
public sealed class TestingWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// The running factory for the assembly. Set when the fixture starts.
    /// </summary>
    public static TestingWebApplicationFactory Instance { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("FunctionalTesting");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });
    }

    public ValueTask InitializeAsync()
    {
        // Tests never reload configuration. A file watcher on appsettings can hang on macOS when the system is busy.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");

        // Start the host now, so startup work is done before the first test
        _ = Services;
        Instance = this;
        return ValueTask.CompletedTask;
    }
}
