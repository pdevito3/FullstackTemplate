namespace FullstackTemplate.UnitTests.Resources;

using System.Globalization;
using System.Text.RegularExpressions;
using FullstackTemplate.UnitTests.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

public partial class CSharpReplPreludeTests
{
    private static readonly CSharpCompilationOptions ScriptOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        .WithUsings("System", "System.Collections.Generic", "System.Linq", "System.Threading.Tasks");

    private const string PreludePath = ".agents/skills/csharprepl/prelude.csx";
    private const string SkillPath = ".agents/skills/csharprepl/SKILL.md";

    [Test]
    public void a_using_that_does_not_compile_is_listed_with_its_line()
    {
        // Arrange
        const string prelude = """
            // Run once for each server process.
            using System.Threading;
            using FullstackTemplate.Server.NoSuchNamespace;
            using FullstackTemplate.Server.Databases;

            "prelude loaded"
            """;

        // Act
        var faults = FaultsOf(prelude);

        // Assert
        faults.ShouldHaveSingleItem().ShouldStartWith(
            $"{PreludePath} line 3: 'using FullstackTemplate.Server.NoSuchNamespace;' does not compile (CS0234)");
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

    [Test]
    public void a_command_that_names_a_moved_type_is_listed()
    {
        // Arrange
        var prelude = File.ReadAllText(Path.Combine(FindRepositoryRoot(), PreludePath));
        const string skill = """
            NO_COLOR=1 dotnet csharprepl connect <pid> --eval 'await InScope<MovedDbContext, int>(db => db.Tenants.CountAsync())'
            """;

        // Act
        var faults = CommandsOf(skill).SelectMany(command => FaultsOf(prelude, command)).ToList();

        // Assert
        faults.ShouldHaveSingleItem().ShouldStartWith(
            $"{SkillPath}: 'await InScope<MovedDbContext, int>(db => db.Tenants.CountAsync())' does not compile (CS0246)");
    }

    [Test]
    public void the_commands_of_the_skill_compile_after_the_prelude()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var prelude = File.ReadAllText(Path.Combine(root, PreludePath));
        var skill = File.ReadAllText(Path.Combine(root, SkillPath));
        var commands = CommandsOf(skill);

        // Act
        var faults = commands.SelectMany(command => FaultsOf(prelude, command)).ToList();

        // Assert
        commands.Count.ShouldBe(CommandLineRegex().Count(skill), "A command in SKILL.md has a form that this test does not read.");
        commands.ShouldContain(command => command.Contains("InScope<AppDbContext, int>"));
        faults.ShouldBeEmpty();
    }

    /// <summary>
    /// Reads the C# of each `--eval` and each heredoc. A line that starts with # is a command of the REPL, not C#.
    /// </summary>
    private static List<string> CommandsOf(string skill) =>
        [.. CommandRegex().Matches(skill)
            .Select(match => string.Join('\n', match.Groups["code"].Value.Split('\n').Where(line => !line.TrimStart().StartsWith('#'))))];

    [GeneratedRegex(@"connect <pid> --eval '(?<code>[^']+)'|connect <pid> --streamPipedInput <<'EOF'\n(?<code>.*?)\n\s*EOF", RegexOptions.Singleline)]
    private static partial Regex CommandRegex();

    [GeneratedRegex(@"connect <pid> (?!--eval-file)")]
    private static partial Regex CommandLineRegex();

    private static CSharpCompilation CompilationOf(string prelude) =>
        CSharpCompilation.CreateScriptCompilation(
            "Prelude", ScriptTreeOf(prelude), PlatformReferences.All, ScriptOptions, globalsType: typeof(ReplHostGlobals));

    private static SyntaxTree ScriptTreeOf(string text) =>
        CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(kind: SourceCodeKind.Script));

    private static List<string> FaultsOf(string prelude)
    {
        var compilation = CompilationOf(prelude);

        return [.. compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => FaultOf(diagnostic, $"{PreludePath} line {diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}", compilation.SyntaxTrees.Single()))];
    }

    // A command is the next submission of the session, as in the real REPL: the usings and helpers of the prelude apply to it.
    private static List<string> FaultsOf(string prelude, string command)
    {
        var tree = ScriptTreeOf(command);
        var compilation = CSharpCompilation.CreateScriptCompilation(
            "Command", tree, PlatformReferences.All, ScriptOptions, CompilationOf(prelude), globalsType: typeof(ReplHostGlobals));

        return [.. compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => FaultOf(diagnostic, SkillPath, tree))];
    }

    private static string FaultOf(Diagnostic diagnostic, string place, SyntaxTree tree)
    {
        var line = diagnostic.Location.GetLineSpan().StartLinePosition.Line;
        var text = tree.GetText().Lines[line].ToString().Trim();
        return $"{place}: '{text}' does not compile ({diagnostic.Id}). {diagnostic.GetMessage(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Stands in for ConnectorGlobals of the csharprepl tool. Only Get is here, because the prelude and the commands of the skill use only Get.
    /// </summary>
    public sealed class ReplHostGlobals
    {
        public T Get<T>() => throw new NotSupportedException("The test only compiles the code.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not find FullstackTemplate.slnx above the test output directory.");
    }
}
