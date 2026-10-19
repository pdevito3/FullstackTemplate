namespace FullstackTemplate.UnitTests.Resources;

using System.Diagnostics;
using Shouldly;

/// <summary>
/// StartupHookSupport=false is the main control that keeps CSharpRepl out of a deployed server (see docs/csharp-repl.md).
/// </summary>
public class StartupHookSupportTests
{
    [Theory]
    [InlineData("Release")]
    [InlineData("Staging")]
    public async Task non_debug_builds_turn_startup_hooks_off(string configuration)
    {
        // Arrange
        var serverProject = Path.Combine(FindRepositoryRoot(), "FullstackTemplate.Server", "FullstackTemplate.Server.csproj");

        // Act
        var value = await GetMsBuildPropertyAsync(serverProject, "StartupHookSupport", configuration);

        // Assert
        value.ShouldBe("false");
    }

    private static async Task<string> GetMsBuildPropertyAsync(string project, string property, string configuration)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "msbuild", project, $"-getProperty:{property}", $"-p:Configuration={configuration}" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        process.ExitCode.ShouldBe(0, await stderr);
        return (await stdout).Trim();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not find FullstackTemplate.slnx above the test output directory.");
    }
}
