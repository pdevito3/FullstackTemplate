namespace FullstackTemplate.AppHost;

using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Serilog;

/// <summary>
/// Lets CSharpRepl attach to a running project. See docs/csharp-repl.md.
/// </summary>
public static class CSharpRepl
{
    private const string ExportPrefix = "export ";
    private const string StartupHooksVariable = "DOTNET_STARTUP_HOOKS";

    /// <summary>
    /// Adds the CSharpRepl startup hook to the project. Only in run mode with the Development
    /// environment. Publish mode and other environments get no hook.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithCSharpRepl(this IResourceBuilder<ProjectResource> project)
    {
        var builder = project.ApplicationBuilder;
        if (!builder.ExecutionContext.IsRunMode || !builder.Environment.IsDevelopment())
            return project;

        // The callback runs when the resource starts, so `connect init` does not delay the AppHost startup.
        // The callback runs again on each restart of the resource. Lazy keeps the lookup to one run.
        var hookVariables = new Lazy<Task<IReadOnlyDictionary<string, string>>>(
            () => ResolveHookVariablesAsync(builder.AppHostDirectory));

        return project.WithEnvironment(async context =>
        {
            foreach (var (name, value) in await hookVariables.Value)
                context.EnvironmentVariables[name] = value;
        });
    }

    /// <summary>
    /// Reads the <c>export NAME="value"</c> lines that <c>csharprepl connect init --shell bash</c> prints.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> ParseExports(string output)
    {
        return output
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(ExportPrefix, StringComparison.Ordinal))
            .Select(line => line[ExportPrefix.Length..].Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0].Length > 0)
            .ToDictionary(parts => parts[0], parts => parts[1].Trim('"'));
    }

    // `csharprepl connect init` prints the hook variables as shell exports. The tool is pinned in dotnet-tools.json.
    private static async Task<IReadOnlyDictionary<string, string>> ResolveHookVariablesAsync(string workingDirectory)
    {
        var none = new Dictionary<string, string>();
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { "tool", "run", "csharprepl", "connect", "init", "--shell", "bash" },
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                Log.Warning("CSharpRepl is disabled: `csharprepl connect init` did not finish in 30 seconds");
                return none;
            }

            var output = $"{await stdout}{await stderr}".Trim();
            var variables = ParseExports(await stdout);

            if (process.ExitCode != 0 || !variables.TryGetValue(StartupHooksVariable, out var hookPath))
            {
                Log.Warning("CSharpRepl is disabled. Run `dotnet tool restore` to install it. Output: {Output}", output);
                return none;
            }

            if (!File.Exists(hookPath))
            {
                Log.Warning("CSharpRepl is disabled: the startup hook {HookPath} does not exist. Output: {Output}", hookPath, output);
                return none;
            }

            return variables;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "CSharpRepl is disabled: `csharprepl connect init` failed");
            return none;
        }
    }
}
