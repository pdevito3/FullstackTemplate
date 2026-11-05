#!/usr/bin/env dotnet
// The new-feature job of CI. It runs scripts/new-feature.cs for two sample entities, then lints, builds, and tests the
// result, on a throwaway copy of the checkout. See job.cs.
//
//   dotnet run scripts/ci/new-feature.cs
// example:start
//   dotnet run scripts/ci/new-feature.cs -- --examples remove
// example:end
//
// The tools: git, the .NET SDK, Docker, and buf.
// example:start
// With --examples remove, also pnpm.
// example:end

#:include job.cs

// Nothing publishes this script, so it needs no native AOT packages.
#:property PublishAot=false

Dictionary<string, Step[]> examples = new()
{
    ["keep"] = [],
    // example:start
    // scripts/remove-examples.cs builds the SPA again, so this choice needs pnpm
    ["remove"] =
    [
        new("Remove the examples", copy =>
        {
            copy.RequireTool("pnpm", "--version", "Install pnpm: https://pnpm.io/installation");
            copy.Run("dotnet", "run", "scripts/remove-examples.cs");
            copy.CommitAll("Remove the examples");
        }),
    ],
    // example:end
};

var choice = args switch
{
    [] => "keep",
    ["--examples", var value] => value,
    _ => "",
};
if (!examples.TryGetValue(choice, out var examplesSteps))
{
    Console.Error.WriteLine($"Usage: dotnet run scripts/ci/new-feature.cs -- [--examples {string.Join('|', examples.Keys)}]");
    return 1;
}

const string CarrierFields = "Name:text, Notes:text?, Code:text:public, MaxLoad:int, IsActive:bool, ReceivedOn:date, CountedAt:instant, ServiceDay:zoned-date";
const string ShipmentFields = "Label:text:public, Carrier:ref, Memo:text?, Count:int, Fragile:bool, SeenAt:instant";

void GenerateSamples(JobCopy copy)
{
    copy.Run("dotnet", "run", "scripts/new-feature.cs", "--", "Carrier", "--entrypoints", "http,mcp", "--fields", CarrierFields);
    copy.Run("dotnet", "run", "scripts/new-feature.cs", "--", "Shipment", "--entrypoints", "http,grpc,mcp", "--fields", ShipmentFields);
}

return Job.Run("new-feature",
[
    new("Check the tools", copy =>
    {
        copy.RequireTool("buf", "--version", "Install the buf version that .github/workflows/ci.yml names: https://buf.build/docs/cli/installation/");
        copy.RequireTool("docker", "info", "The .NET tests start Postgres with Testcontainers. Start Docker.");
    }),
    .. examplesSteps,
    new("Generate the sample features", GenerateSamples),
    new("Fail if a second run changes a file", copy =>
    {
        copy.CommitAll("Add the sample features");
        GenerateSamples(copy);
        copy.FailOnChanges("A second run of scripts/new-feature.cs with the same input changed the files above.");
    }),
    new("Fail if a run again after a delete keeps a migration that does not match", copy =>
    {
        var added = copy.Output("git", "show", "--name-only", "--diff-filter=A", "--format=", "HEAD")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(file => file.Contains("Carrier", StringComparison.Ordinal) && !file.Contains("/Migrations/", StringComparison.Ordinal))
            .ToList();
        if (added.Count == 0)
            throw new StepFailure("The last commit added no Carrier file to delete.");
        foreach (var file in added)
            copy.DeleteFile(file);

        var (exitCode, output, error) = copy.Capture("dotnet", "run", "scripts/new-feature.cs", "--", "Carrier", "--fields", "Name:text, Rating:int");
        Console.WriteLine(output + error);
        if (exitCode == 0)
            throw new StepFailure("The script ran again on a kept migration and exited 0.");
        if (!(output + error).Contains("AddCarriers", StringComparison.Ordinal))
            throw new StepFailure("The script stopped, but its message does not name the migration AddCarriers.");

        copy.Run("git", "reset", "--quiet", "--hard", "HEAD");
        copy.Run("git", "clean", "--force", "-d", "--quiet");
    }),
    new("Lint the gRPC contract", copy => copy.Run("buf", "lint")),
    new("Build the solution", copy => copy.Run("dotnet", "build", "FullstackTemplate.slnx", "-p:OpenApiGenerateDocuments=false")),
    new("Run the .NET tests", copy => copy.Run("dotnet", "test", "--solution", "FullstackTemplate.slnx", "--no-build", "-p:OpenApiGenerateDocuments=false", "-p:SkipBffTests=true")),
]);
