namespace FullstackTemplate.FunctionalTests.TestUtilities;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Resources.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// The built Server in a process of its own, with a role and a database, as a deploy runs it. A second host in the test
/// process would share the static Serilog logger and the span listeners of the test server, so a role test runs the
/// Server out of process. The process gets the storage and auth settings of the test server from the environment.
/// </summary>
public sealed class ServerProcess : IAsyncDisposable
{
    private static readonly TimeSpan StartLimit = TimeSpan.FromMinutes(1);

    /// <summary>The build output of the Server, in the configuration and the framework of the test build.</summary>
    private static readonly string ServerDirectory = FindServerDirectory();

    private readonly Process _process;
    private readonly ConcurrentQueue<string> _output = new();

    private ServerProcess(Process process, Uri address)
    {
        _process = process;
        Client = new HttpClient { BaseAddress = address };
    }

    /// <summary>A client on the HTTP endpoint of the process.</summary>
    public HttpClient Client { get; }

    /// <summary>Each line that the process wrote to its console, in order.</summary>
    public IReadOnlyList<string> Output => [.. _output];

    /// <summary>Starts the Server and waits until <c>/health</c> gives 200, so the migration is done.</summary>
    public static async Task<ServerProcess> StartAsync(ServerRole role, string connectionString)
    {
        var address = new Uri($"http://127.0.0.1:{FreePort()}");
        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { Path.Combine(ServerDirectory, "FullstackTemplate.Server.dll") },
            WorkingDirectory = ServerDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["ASPNETCORE_ENVIRONMENT"] = Environments.Production;
        start.Environment["Server__Role"] = role.ToString();
        start.Environment[$"ConnectionStrings__{DatabaseConsts.DatabaseName}"] = connectionString;
        start.Environment["Kestrel__Endpoints__http__Url"] = address.GetLeftPart(UriPartial.Authority);
        start.Environment["Kestrel__Endpoints__grpc__Url"] = "http://127.0.0.1:0";
        // The test server turns this check off. A deployed Server checks its database in /health.
        start.Environment.Remove("Aspire__Npgsql__EntityFrameworkCore__PostgreSQL__DisableHealthChecks");

        var server = new ServerProcess(new Process { StartInfo = start }, address);
        server._process.OutputDataReceived += server.Collect;
        server._process.ErrorDataReceived += server.Collect;
        server._process.Start();
        server._process.BeginOutputReadLine();
        server._process.BeginErrorReadLine();
        try
        {
            await server.WaitUntilHealthyAsync();
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        _process.Dispose();
        Client.Dispose();
    }

    private async Task WaitUntilHealthyAsync()
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            if (_process.HasExited)
                throw new InvalidOperationException(
                    $"The Server stopped with exit code {_process.ExitCode}.{Environment.NewLine}{string.Join(Environment.NewLine, Output)}");
            if (waited.Elapsed > StartLimit)
                throw new TimeoutException(
                    $"The Server was not healthy after {StartLimit}.{Environment.NewLine}{string.Join(Environment.NewLine, Output)}");
            try
            {
                using var health = await Client.GetAsync(ApiRoutes.Health);
                if (health.StatusCode == HttpStatusCode.OK)
                    return;
            }
            catch (HttpRequestException)
            {
                // Kestrel does not listen yet
            }
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
    }

    private void Collect(object sender, DataReceivedEventArgs line)
    {
        if (line.Data is not null)
            _output.Enqueue(line.Data);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string FindServerDirectory()
    {
        // The test output is tests/<project>/bin/<configuration>/<framework>/
        var output = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        var (framework, configuration) = (output.Name, output.Parent!.Name);
        var root = output;
        while (!File.Exists(Path.Combine(root.FullName, "FullstackTemplate.slnx")))
            root = root.Parent ?? throw new InvalidOperationException("The test found no FullstackTemplate.slnx above its folder.");

        return Path.Combine(root.FullName, "FullstackTemplate.Server", "bin", configuration, framework);
    }
}
