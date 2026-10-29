namespace FullstackTemplate.UnitTests.Scripts;

using System.Diagnostics;
using Shouldly;

/// <summary>
/// scripts/new-feature.cs refuses a ref that the code it writes cannot hold, before it writes a file. Each case runs
/// the script in the repo, so a run that does not refuse would change the checkout.
/// </summary>
[NotInParallel(nameof(NewFeatureTextTests))]
public class NewFeatureRefusalTests
{
    [Test]
    [Arguments("Title:text, User:ref", "A ref to User is not possible")]
    [Arguments("Title:text, User:ref?", "A ref cannot be optional. Remove the '?' from User.")]
    [Arguments("Title:text, UserId:ref", "with no Id: User:ref.")]
    // example:start
    [Arguments("Title:text, Venue:ref", "Venue is an example")]
    [Arguments("Title:text, Workshop:ref", "A ref can name only an entity that is right under the tenant. Workshop is under Venue.")]
    // example:end
    public async Task a_ref_that_the_written_code_cannot_hold_is_refused_before_a_write(string fields, string message)
    {
        // Act
        var (exitCode, output) = await RunAsync("Assignment", fields);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldContain(message);
        output.ShouldNotContain("Wrote ");
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string entity, string fields)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "run", "scripts/new-feature.cs", "--", entity, "--fields", fields })
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start).ShouldNotBeNull();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output + await error);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
