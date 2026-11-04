// Runs the steps of a CI job on a throwaway copy of the checkout. CI and a local run take this same path, so that they
// run the same steps, and the checkout does not change. The scripts in this folder include this file.
//
// The copy holds what git would commit from the checkout: each tracked and each untracked file that git does not ignore,
// with their uncommitted changes. So a run before a commit tests the current work. A pass deletes the copy. A failure
// keeps it, for its logs and test reports.

using System.ComponentModel;
using System.Diagnostics;

internal sealed record Step(string Name, Action<JobCopy> Run);

internal sealed class StepFailure(string message) : Exception(message);

internal static class Job
{
    public static int Run(string job, string? copyTo, IReadOnlyList<Step> steps)
    {
        // GitHub sets CI on each runner. The frontend tools and the test containers read it: a test with test.only fails
        // the Playwright suite only with CI set.
        Environment.SetEnvironmentVariable("CI", "true");
        Environment.SetEnvironmentVariable("GIT_PAGER", "cat");
        // A git hook exports these. A git command in the copy would then write to the repo of the checkout.
        foreach (var variable in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE" })
            Environment.SetEnvironmentVariable(variable, null);

        if (copyTo is not null && Directory.Exists(copyTo) && Directory.EnumerateFileSystemEntries(copyTo).Any())
        {
            Console.Error.WriteLine($"The folder {copyTo} has files. Give --copy-to an empty folder.");
            return 1;
        }

        var folder = copyTo is null
            ? Directory.CreateTempSubdirectory($"ci-{job}-").FullName
            : Directory.CreateDirectory(copyTo).FullName;
        var copy = new JobCopy(folder);
        var total = Stopwatch.StartNew();

        foreach (var step in new[] { new Step("Copy the checkout", c => c.FillFromCheckout()) }.Concat(steps))
        {
            var failure = RunStep(step, copy);
            if (failure is not null)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"{job} failed at the step \"{step.Name}\". {failure}");
                Console.Error.WriteLine($"The copy of the checkout, with its logs and test reports, stays at {folder}. Delete it when you are done.");
                if (OnGitHub)
                    Console.Error.WriteLine($"::error::{job} failed at the step \"{step.Name}\". {failure}");
                return 1;
            }
        }

        Console.WriteLine($"{job} passed in {total.Elapsed:m\\:ss}.");
        DeleteCopy(folder);
        return 0;
    }

    private static bool OnGitHub => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

    private static string? RunStep(Step step, JobCopy copy)
    {
        Console.WriteLine(OnGitHub ? $"::group::{step.Name}" : $"=== {step.Name}");
        var time = Stopwatch.StartNew();
        try
        {
            step.Run(copy);
            return null;
        }
        catch (Exception exception) when (exception is StepFailure or IOException or UnauthorizedAccessException)
        {
            return exception.Message;
        }
        finally
        {
            if (OnGitHub)
                Console.WriteLine("::endgroup::");
            Console.WriteLine($"=== {step.Name}: {time.Elapsed:m\\:ss}");
        }
    }

    private static void DeleteCopy(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not delete the copy at {folder}: {exception.Message}");
        }
    }

    // Each option takes a value. Returns null when an argument is not one of the options.
    public static Dictionary<string, string>? ParseOptions(string[] args, params string[] options)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!options.Contains(args[i]) || i + 1 == args.Length)
                return null;
            values[args[i]] = args[i + 1];
        }
        return values;
    }
}

internal sealed class JobCopy(string root)
{
    public void FillFromCheckout()
    {
        var entryFolder = (string)AppContext.GetData("EntryPointFileDirectoryPath")!;
        var checkout = Output(entryFolder, "git", "rev-parse", "--show-toplevel").Trim();
        if (Path.GetFullPath(root).StartsWith(checkout + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new StepFailure($"The copy at {root} is in the checkout, and it must not change the checkout. Give another folder.");

        var files = Output(checkout, "git", "ls-files", "-z", "--cached", "--others", "--exclude-standard")
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Distinct();

        foreach (var file in files)
        {
            var source = new FileInfo(Path.Combine(checkout, file));
            var target = Path.Combine(root, file);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (source.LinkTarget is { } link)
                File.CreateSymbolicLink(target, link);
            else if (source.Exists)
                source.CopyTo(target);
        }

        // The hooks, the ignores, and the signing of a machine must not change what the copy commits
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", "/dev/null");
        Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");

        // --force: a tracked file can match .gitignore, and the copy must have it
        Run("git", "init", "--quiet");
        Run("git", "add", "--all", "--force");
        Commit("The checkout");
    }

    public void Run(string command, params string[] arguments) => RunIn(".", command, arguments);

    public void RunIn(string folder, string command, params string[] arguments)
    {
        var line = $"{command} {string.Join(' ', arguments)}";
        Console.WriteLine($"> {line}");
        using var process = Start(StartInfo(Path.Combine(root, folder), command, arguments), command);
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new StepFailure($"'{line}' exited with {process.ExitCode}.");
    }

    public (int ExitCode, string Output) Capture(string command, params string[] arguments) =>
        Capture(root, command, arguments);

    public string Output(string command, params string[] arguments) => Output(root, command, arguments);

    public int Status(string command, params string[] arguments)
    {
        Console.WriteLine($"> {command} {string.Join(' ', arguments)}");
        using var process = Start(StartInfo(root, command, arguments), command);
        process.WaitForExit();
        return process.ExitCode;
    }

    public void RequireTool(string tool, string argument, string help)
    {
        int exitCode;
        try
        {
            exitCode = Capture(root, tool, argument).ExitCode;
        }
        catch (StepFailure)
        {
            exitCode = -1;
        }
        if (exitCode != 0)
            throw new StepFailure($"'{tool} {argument}' did not work. {help}");
    }

    public void CommitAll(string message)
    {
        Run("git", "add", "--all");
        Commit(message);
    }

    private void Commit(string message) =>
        Run("git", "-c", "user.name=ci", "-c", "user.email=ci@localhost", "commit", "--quiet", "--message", message);

    public void FailOnChanges(string reason)
    {
        Run("git", "add", "--intent-to-add", "--all");
        if (Status("git", "diff", "--exit-code", "--stat") != 0)
            throw new StepFailure(reason);
    }

    public void DeleteFile(string file) => File.Delete(Path.Combine(root, file));

    private static string Output(string folder, string command, params string[] arguments)
    {
        var (exitCode, output) = Capture(folder, command, arguments);
        return exitCode == 0
            ? output
            : throw new StepFailure($"'{command} {string.Join(' ', arguments)}' exited with {exitCode} in {folder}: {output.Trim()}");
    }

    private static (int ExitCode, string Output) Capture(string folder, string command, params string[] arguments)
    {
        var startInfo = StartInfo(folder, command, arguments);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        using var process = Start(startInfo, command);
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output + error.Result);
    }

    private static ProcessStartInfo StartInfo(string folder, string command, string[] arguments)
    {
        // On Windows, pnpm is a .cmd file, and Process.Start finds only an .exe
        var startInfo = OperatingSystem.IsWindows() && command == "pnpm"
            ? new ProcessStartInfo("cmd.exe", ["/c", command, .. arguments])
            : new ProcessStartInfo(command, arguments);
        startInfo.WorkingDirectory = folder;
        return startInfo;
    }

    private static Process Start(ProcessStartInfo startInfo, string command)
    {
        try
        {
            return Process.Start(startInfo) ?? throw new StepFailure($"Could not start {command}.");
        }
        catch (Win32Exception exception)
        {
            throw new StepFailure($"Could not start {command}: {exception.Message}");
        }
    }
}
