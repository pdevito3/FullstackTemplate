#!/usr/bin/env dotnet
// The without-examples job of CI. It runs scripts/remove-examples.cs, checks the result, then lints, builds, and tests the
// app that is left, on a throwaway copy of the checkout. See docs/examples.md and ../job.cs.
//
//   dotnet run scripts/ci/examples/without-examples.cs
//
// The tools: git, the .NET SDK, pnpm, and Docker.
//
// This file is in an examples folder, so scripts/remove-examples.cs deletes it with the job.

#:include ../job.cs

if (args.Length > 0)
{
    Console.Error.WriteLine("Usage: dotnet run scripts/ci/examples/without-examples.cs");
    return 1;
}

const string ExampleNames = "weather|forecast|filter-demo|components-showcase|workshop|wkatt|(^|[^e])venue|example:(start|end)";
string[] exampleFolders = ["Examples", "examples", "(examples)"];

return Job.Run("without-examples",
[
    new("Check the tools", copy =>
    {
        copy.RequireTool("pnpm", "--version", "Install pnpm: https://pnpm.io/installation");
        copy.RequireTool("docker", "info", "The .NET tests start Postgres with Testcontainers. Start Docker.");
    }),
    new("Remove the examples", copy => copy.Run("dotnet", "run", "scripts/remove-examples.cs")),
    // scripts/remove-examples.cs changes only a clean tree, so this step commits the first run
    new("Fail if a second run changes a file", copy =>
    {
        copy.CommitAll("Remove the examples");
        copy.Run("dotnet", "run", "scripts/remove-examples.cs");
        copy.FailOnChanges("A second run of scripts/remove-examples.cs changed the files above.");
    }),
    new("Fail on example code that the script left", copy =>
    {
        var folders = copy.Output("git", "ls-files")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(file => file.Split('/')[..^1].Intersect(exampleFolders).Any())
            .ToList();
        if (folders.Count > 0)
            throw new StepFailure($"These files are in an example folder: {string.Join(", ", folders)}");

        // git grep exits with 1 when it finds nothing, 0 on a match, and a higher code on an error
        var status = copy.Status("git", "grep", "-n", "-I", "-i", "-E", ExampleNames,
            "--", ":!scripts/remove-examples.cs", ":!docs/examples.md", ":!**/Migrations/*.Designer.cs");
        if (status == 0)
            throw new StepFailure("The lines above name an example, and the app with no examples keeps them. Put each line in an example section, or reword it.");
        if (status != 1)
            throw new StepFailure($"git grep exited with {status}.");
    }),
    // The CI script of the new-feature job runs from the checkout with its example sections, so no other step builds the form that an app gets
    new("Fail if scripts/ci/new-feature.cs does not build with no examples", copy =>
    {
        var (exitCode, output, error) = copy.Capture("dotnet", "run", "scripts/ci/new-feature.cs", "--", "--help");
        if (exitCode != 1 || !error.Contains("Usage:", StringComparison.Ordinal))
            throw new StepFailure($"Expected its usage message and exit code 1, and got exit code {exitCode}: {output}{error}");
    }),
    // The frontend tests run first, because after the .NET tests on the same runner the Vitest browser can hang at its start
    new("Lint the frontend", copy => copy.RunIn("frontend", "pnpm", "lint")),
    new("Install Chromium", copy => copy.RunIn("frontend", "pnpm", "exec", "playwright", "install", "chromium")),
    new("Test the frontend", copy => copy.RunIn("frontend", "pnpm", "test")),
    new("Run the functional tests", copy => copy.RunIn("frontend", "pnpm", "test:e2e")),
    new("Build the solution", copy => copy.Run("dotnet", "build", "FullstackTemplate.slnx")),
    new("Run the .NET tests", copy => copy.Run("dotnet", "test", "--solution", "FullstackTemplate.slnx", "--no-build")),
]);
