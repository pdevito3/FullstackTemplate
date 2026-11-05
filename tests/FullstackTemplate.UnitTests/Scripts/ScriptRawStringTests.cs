namespace FullstackTemplate.UnitTests.Scripts;

using FullstackTemplate.SourceGenerators;
using FullstackTemplate.UnitTests.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

/// <summary>
/// The build runs FSTSTR001 on each project of the solution, but not on a file-based script under scripts/ or on the
/// analyzer project itself. This test runs the rule on those files.
/// </summary>
public class ScriptRawStringTests
{
    [Test]
    public async Task the_scripts_and_the_analyzers_follow_fststr001()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)
            .WithFeatures([new("FileBasedProgram", "true")]);
        var trees = Directory.EnumerateFiles(Path.Combine(root, "scripts"), "*.cs")
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "FullstackTemplate.SourceGenerators"), "*.cs"))
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, Path.GetRelativePath(root, file)))
            .ToList();
        trees.ShouldNotBeEmpty();
        var compilation = CSharpCompilation.Create("Scripts", trees, PlatformReferences.All);

        // Act
        var diagnostics = await compilation
            .WithAnalyzers([new RawStringAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current!.Execution.CancellationToken);

        // Assert
        diagnostics.Select(d => $"{d.Location.GetLineSpan().Path}:{d.Location.GetLineSpan().StartLinePosition.Line + 1}").ShouldBeEmpty();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not find FullstackTemplate.slnx above the test output directory.");
    }
}
