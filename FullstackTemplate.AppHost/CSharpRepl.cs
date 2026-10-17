namespace FullstackTemplate.AppHost;

using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Serilog;

/// <summary>
/// Lets CSharpRepl attach to a running project. See docs/csharp-repl.md.
/// </summary>
public static class CSharpRepl
{
    private const string StartupHookExport = "export DOTNET_STARTUP_HOOKS=";

    /// <summary>
    /// Adds the CSharpRepl startup hook to the project. Only in run mode with the Development
    /// environment. Publish mode and other environments get no hook.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithCSharpRepl(this IResourceBuilder<ProjectResource> project)
    {
        var builder = project.ApplicationBuilder;
        if (!builder.ExecutionContext.IsRunMode || !builder.Environment.IsDevelopment())
            return project;

        var hookPath = ResolveStartupHook(builder.AppHostDirectory);
        if (hookPath is null)
            return project;

        return project
            .WithEnvironment("DOTNET_STARTUP_HOOKS", hookPath)
            .WithEnvironment("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "CSharpRepl.InjectedHook");
    }

    // `csharprepl connect init` prints the hook path as a shell export. The tool is pinned in dotnet-tools.json.
    private static string? ResolveStartupHook(string workingDirectory)
    {
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
            if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
            {
                process.Kill(entireProcessTree: true);
                Log.Warning("CSharpRepl is disabled: `csharprepl connect init` did not finish in 30 seconds");
                return null;
            }

            var hookPath = stdout.Result
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.StartsWith(StartupHookExport, StringComparison.Ordinal))
                .Select(line => line[StartupHookExport.Length..].Trim('"'))
                .FirstOrDefault();

            if (process.ExitCode != 0 || hookPath is null || !File.Exists(hookPath))
            {
                Log.Warning("CSharpRepl is disabled. Run `dotnet tool restore` to install it. Output: {Output}",
                    $"{stdout.Result}{stderr.Result}".Trim());
                return null;
            }

            return hookPath;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "CSharpRepl is disabled. Run `dotnet tool restore` to install it");
            return null;
        }
    }
}
