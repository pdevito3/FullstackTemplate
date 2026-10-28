namespace FullstackTemplate.UnitTests.AppHost;

using System.Text.RegularExpressions;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using FullstackTemplate.AppHost;
using FullstackTemplate.AppHost.AuthProviders;
using Shouldly;

// The port table in README.md is the list that developers read, so the test holds the AppHost model to it.
public partial class PortTableTests
{
    private const string EnvironmentVariable = "ASPNETCORE_ENVIRONMENT";
    private static string? environmentBefore;

    private static readonly HashSet<(string Provider, string Resource, string Endpoint)> NoFixedPort =
    [
        // init:start - scripts/init.cs deletes the provider of the conformance suite when it renames the app
        // The conformance BFF binds 0.0.0.0 with no proxy, so Aspire must pick a port that is free on all addresses.
        // See ConformanceAuthProvider.
        ("Conformance", "bff", "https"),
        // init:end
    ];

    public static IEnumerable<string> ProviderNames => AuthProviderCatalog.All.Select(provider => provider.Name);

    // A normal run starts the AppHost in Development (its launchSettings.json). Aspire reads the Kestrel endpoints of
    // the server from appsettings.{environment}.json, with the environment from this variable of the AppHost process.
    [Before(Class)]
    public static void RunInDevelopment()
    {
        environmentBefore = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Environment.SetEnvironmentVariable(EnvironmentVariable, "Development");
    }

    [After(Class)]
    public static void RestoreEnvironment() => Environment.SetEnvironmentVariable(EnvironmentVariable, environmentBefore);

    [Test]
    public void the_port_table_gives_each_port_once()
    {
        // Act
        var ports = ReadmePortTable().Select(row => row.Port).ToList();

        // Assert
        ports.ShouldNotBeEmpty();
        ports.ShouldBeUnique("Two rows of the port table in README.md have the same port.");
    }

    [Test]
    [MethodDataSource(nameof(ProviderNames))]
    public async Task each_endpoint_of_a_normal_run_has_its_port_from_the_port_table(string name)
    {
        // Arrange
        var table = ReadmePortTable().ToDictionary(row => (row.Resource, row.Endpoint), row => row.Port);

        // Act
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(
            typeof(Isolation), [$"--AuthProvider={name}"], (options, _) => options.DisableDashboard = true);

        // Assert
        var endpoints = builder.Resources
            .SelectMany(resource => resource.Annotations.OfType<EndpointAnnotation>(), (resource, endpoint) => (Resource: resource.Name, Endpoint: endpoint.Name, endpoint.Port))
            .Where(endpoint => !NoFixedPort.Contains((name, endpoint.Resource, endpoint.Endpoint)))
            .ToList();
        endpoints.ShouldNotBeEmpty();
        foreach (var (resource, endpoint, port) in endpoints)
        {
            table.TryGetValue((resource, endpoint), out var expected).ShouldBeTrue(
                $"The {endpoint} endpoint of {resource} needs a row in the port table in README.md.");
            port.ShouldBe(expected,
                $"The {endpoint} endpoint of {resource} needs the fixed port {expected} of the port table in README.md, so that a normal run uses the same port at each start.");
        }
    }

    private static IEnumerable<(int Port, string Resource, string Endpoint)> ReadmePortTable()
    {
        var readme = File.ReadAllText(Path.Combine(RepositoryRoot(), "README.md"));
        // A row is "| 24711 | `bff` | https |". The resource is the first name in backticks, or the plain text.
        return PortRow().Matches(readme).Select(row => (
            int.Parse(row.Groups["port"].Value, System.Globalization.CultureInfo.InvariantCulture),
            BacktickName().Match(row.Groups["resource"].Value) is { Success: true } code ? code.Groups[1].Value : row.Groups["resource"].Value.Trim(),
            row.Groups["endpoint"].Value.Trim()));
    }

    [GeneratedRegex(@"^\| (?<port>\d+) \| (?<resource>[^|]+) \| (?<endpoint>[^|]+) \|$", RegexOptions.Multiline)]
    private static partial Regex PortRow();

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex BacktickName();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory.GetFiles("*.slnx").Length == 0)
            directory = directory.Parent ?? throw new InvalidOperationException("The test found no .slnx file above its folder.");

        return directory.FullName;
    }
}
