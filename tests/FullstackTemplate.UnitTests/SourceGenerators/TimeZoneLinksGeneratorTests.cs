namespace FullstackTemplate.UnitTests.SourceGenerators;

using System.Runtime.Loader;
using FullstackTemplate.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Shouldly;

public class TimeZoneLinksGeneratorTests
{
    [Test]
    public void each_link_line_of_the_file_becomes_a_link_and_the_other_lines_do_not()
    {
        // Arrange
        const string backward = """
            # Link TARGET LINK-NAME #= TARGET1
            Link Australia/Sydney Australia/ACT #= Australia/Canberra

            Link Asia/Kolkata Asia/Calcutta
            Zone EST5EDT -5:00 USback E%sT
            """;

        // Act
        var links = Links(new FileText("Domain/TimeZones/tzdata/backward", backward));

        // Assert
        links.ShouldBe([("Australia/ACT", "Australia/Sydney"), ("Asia/Calcutta", "Asia/Kolkata")]);
    }

    [Test]
    public void a_project_with_no_backward_file_gets_no_links()
    {
        // Act
        var generated = Run(new FileText("BannedSymbols.txt", "Link Asia/Kolkata Asia/Calcutta"));

        // Assert
        generated.SyntaxTrees.ShouldBeEmpty();
    }

    private static (string Name, string Target)[] Links(AdditionalText file)
    {
        using var image = new MemoryStream();
        Run(file).Emit(image).Success.ShouldBeTrue();
        image.Position = 0;
        var assembly = new AssemblyLoadContext(null, isCollectible: true).LoadFromStream(image);
        return ((string, string)[])assembly.GetType("FullstackTemplate.Server.Domain.TimeZones.TimeZoneLinks")!
            .GetField("All")!.GetValue(null)!;
    }

    private static Compilation Run(AdditionalText file)
    {
        var compilation = CSharpCompilation.Create(
            $"TimeZoneLinks{Guid.NewGuid():N}",
            [],
            PlatformReferences.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver.Create([new TimeZoneLinksGenerator().AsSourceGenerator()], additionalTexts: [file],
                parseOptions: new CSharpParseOptions(LanguageVersion.Latest))
            .RunGeneratorsAndUpdateCompilation(compilation, out var generated, out _);
        generated.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning).ShouldBeEmpty();
        return generated;
    }

    private sealed class FileText(string path, string text) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
