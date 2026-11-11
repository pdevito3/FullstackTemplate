namespace FullstackTemplate.UnitTests.SourceGenerators;

using System.Runtime.Loader;
using FullstackTemplate.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Shouldly;

public class TimeZoneNamesGeneratorTests
{
    [Test]
    public void each_link_line_becomes_a_link_each_zone_line_becomes_a_zone_and_the_other_lines_do_not()
    {
        // Arrange
        const string backward = """
            # Link TARGET LINK-NAME #= TARGET1
            Link Australia/Sydney Australia/ACT #= Australia/Canberra

            Link Asia/Kolkata Asia/Calcutta
            Zone EST5EDT -5:00 USback E%sT
            # Zone NAME STDOFF RULES FORMAT
            """;

        // Act
        var (links, zones) = Names(new FileText("Domain/TimeZones/tzdata/backward", backward));

        // Assert
        links.ShouldBe([("Australia/ACT", "Australia/Sydney"), ("Asia/Calcutta", "Asia/Kolkata")]);
        zones.ShouldBe(["EST5EDT"]);
    }

    [Test]
    public void the_names_of_backward_and_etcetera_go_into_one_table()
    {
        // Arrange
        var etcetera = new FileText("Domain/TimeZones/tzdata/etcetera", """
            Zone Etc/GMT+5 -5 - %z
            Link Etc/GMT GMT
            """);
        var backward = new FileText("Domain/TimeZones/tzdata/backward", """
            Zone EST5EDT -5:00 USback E%sT
            Link Asia/Kolkata Asia/Calcutta
            """);

        // Act
        var (links, zones) = Names(etcetera, backward);

        // Assert
        links.ShouldBe([("Asia/Calcutta", "Asia/Kolkata"), ("GMT", "Etc/GMT")]);
        zones.ShouldBe(["EST5EDT", "Etc/GMT+5"]);
    }

    [Test]
    public void a_project_with_no_iana_file_gets_no_names()
    {
        // Act
        var generated = Run(new FileText("BannedSymbols.txt", "Link Asia/Kolkata Asia/Calcutta"));

        // Assert
        generated.SyntaxTrees.ShouldBeEmpty();
    }

    private static ((string Name, string Target)[] Links, string[] Zones) Names(params AdditionalText[] files)
    {
        using var image = new MemoryStream();
        Run(files).Emit(image).Success.ShouldBeTrue();
        image.Position = 0;
        var assembly = new AssemblyLoadContext(null, isCollectible: true).LoadFromStream(image);
        var names = assembly.GetType("FullstackTemplate.Server.Domain.TimeZones.TimeZoneNames")!;
        return (((string, string)[])names.GetField("Links")!.GetValue(null)!, (string[])names.GetField("Zones")!.GetValue(null)!);
    }

    private static Compilation Run(params AdditionalText[] files)
    {
        var compilation = CSharpCompilation.Create(
            $"TimeZoneNames{Guid.NewGuid():N}",
            [],
            PlatformReferences.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver.Create([new TimeZoneNamesGenerator().AsSourceGenerator()], additionalTexts: files,
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
