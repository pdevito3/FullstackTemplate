namespace FullstackTemplate.UnitTests.Resources;

using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

/// <summary>
/// The build does not compile the CSharpRepl prelude (see docs/csharp-repl.md). This test compiles all of it against
/// the Server, so a namespace move or a moved type fails here and not in the next REPL session.
/// </summary>
public class CSharpReplPreludeTests
{
    private const string PreludePath = ".agents/skills/csharprepl/prelude.csx";

    [Test]
    public void a_using_that_does_not_compile_is_listed_with_its_line()
    {
        // Arrange
        const string prelude = """
            // Run once for each server process.
            using System.Threading;
            using FullstackTemplate.Server.Services;
            using FullstackTemplate.Server.Databases;

            "prelude loaded"
            """;

        // Act
        var faults = FaultsOf(prelude);

        // Assert
        faults.ShouldHaveSingleItem().ShouldStartWith(
            $"{PreludePath} line 3: 'using FullstackTemplate.Server.Services;' does not compile (CS0234)");
    }

    [Test]
    public void a_helper_that_calls_a_moved_type_is_listed_with_its_line()
    {
        // Arrange
        const string prelude = """
            using Microsoft.Extensions.DependencyInjection;

            async Task<int> Helper()
            {
                await using var scope = Get<IServiceScopeFactory>().CreateSystemScope();
                return 1;
            }
            """;

        // Act
        var faults = FaultsOf(prelude);

        // Assert
        faults.ShouldHaveSingleItem().ShouldStartWith(
            $"{PreludePath} line 5: 'await using var scope = Get<IServiceScopeFactory>().CreateSystemScope();' does not compile (CS1061)");
    }

    [Test]
    public void the_prelude_compiles_against_the_server()
    {
        // Arrange
        var prelude = File.ReadAllText(Path.Combine(FindRepositoryRoot(), PreludePath));

        // Act
        var faults = FaultsOf(prelude);

        // Assert
        faults.ShouldBeEmpty();
    }

    private static List<string> FaultsOf(string prelude)
    {
        var tree = CSharpSyntaxTree.ParseText(prelude, new CSharpParseOptions(kind: SourceCodeKind.Script));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithUsings("System", "System.Collections.Generic", "System.Linq", "System.Threading.Tasks");
        var compilation = CSharpCompilation.CreateScriptCompilation(
            "Prelude", tree, references, options, globalsType: typeof(ReplHostGlobals));

        return [.. compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => FaultOf(diagnostic, tree))];
    }

    private static string FaultOf(Diagnostic diagnostic, SyntaxTree tree)
    {
        var line = diagnostic.Location.GetLineSpan().StartLinePosition.Line;
        var text = tree.GetText().Lines[line].ToString().Trim();
        return $"{PreludePath} line {line + 1}: '{text}' does not compile ({diagnostic.Id}). {diagnostic.GetMessage(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Stands in for ConnectorGlobals of the csharprepl tool. It and the usings of the compilation options give the
    /// prelude what the REPL host gives it.
    /// </summary>
    public sealed class ReplHostGlobals
    {
        public T Get<T>() => throw new NotSupportedException("The test only compiles the prelude.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not find FullstackTemplate.slnx above the test output directory.");
    }
}
