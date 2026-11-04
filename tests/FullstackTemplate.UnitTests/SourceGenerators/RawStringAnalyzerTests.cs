namespace FullstackTemplate.UnitTests.SourceGenerators;

using System.Collections.Immutable;
using System.Globalization;
using FullstackTemplate.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

public class RawStringAnalyzerTests
{
    [Test]
    public async Task a_string_with_an_escaped_quote_gets_fststr001()
    {
        // Act
        var diagnostics = await RunInMethod("""var text = "say \"hi\" now";""");

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTSTR001");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            """"The string has \" in it. Write a raw string literal, for example """say "hi" now""" in place of "say \"hi\" now". Put a text with a new line, or a text that starts or ends with a quote, on the lines between the two delimiters. If an interpolated text has { or }, start it with $$. See docs/backend/code-style.md."""");
        Text(diagnostic).ShouldBe("""
            "say \"hi\" now"
            """);
    }

    [Test]
    [Arguments("""var text = "a\nb";""")]
    [Arguments("""var text = "\nb";""")]
    [Arguments("""var text = "a\n\n";""")]
    [Arguments("""var text = $"{id}\n{id}";""")]
    [Arguments("""var text = $"a\n{id}";""")]
    public async Task a_string_with_a_new_line_before_its_end_gets_fststr001(string statement)
    {
        // Act
        var diagnostics = await RunInMethod(statement);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage(CultureInfo.InvariantCulture).ShouldStartWith("The string has \\n before its end.");
    }

    [Test]
    [Arguments("""var text = $"{id} said \"hi\"!";""")]
    [Arguments("""var text = "say \"hi\" now"u8;""")]
    [Arguments("""var text = "a\nb" + id;""")]
    [Arguments("""var text = "say \"hi\"\n";""")]
    public async Task an_interpolated_or_a_utf8_or_a_joined_string_gets_fststr001(string statement)
    {
        // Act
        var diagnostics = await RunInMethod(statement);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTSTR001");
    }

    [Test]
    public async Task a_verbatim_string_that_spans_lines_gets_fststr001()
    {
        // Act
        var diagnostics = await RunInMethod("""
            var text = @"a
            b";
            var hole = $@"a
            {id}";
            """);

        // Assert
        diagnostics.Length.ShouldBe(2);
        diagnostics.ShouldAllBe(d => d.GetMessage(CultureInfo.InvariantCulture).StartsWith("The verbatim string spans lines.", StringComparison.Ordinal));
    }

    [Test]
    public async Task a_string_in_an_attribute_a_const_and_a_switch_pattern_gets_fststr001()
    {
        // Act
        var diagnostics = await Run("""
            [System.Obsolete("say \"hi\" now")]
            public static class Sample
            {
                public const string Text = "a\nb";

                public static int Run(string id) => id switch
                {
                    "say \"hi\" now" => 1,
                    _ => 0,
                };
            }
            """);

        // Assert
        diagnostics.Length.ShouldBe(3);
        diagnostics.ShouldAllBe(d => d.Id == "FSTSTR001");
    }

    [Test]
    [Arguments("""var text = "plain";""")]
    [Arguments("""var text = "a line\n";""")]
    [Arguments("""var text = "\n";""")]
    [Arguments("""var text = $"{id}\n";""")]
    [Arguments("""var text = "a\\nb";""")]
    [Arguments("""var text = "C:\\temp";""")]
    [Arguments("""var text = @"say ""hi"" now";""")]
    [Arguments("""var text = $"{{id}}";""")]
    [Arguments(""""var text = """say "hi" now""";"""")]
    [Arguments(""""var text = $"""{id} said "hi"!""";"""")]
    public async Task a_string_with_no_hidden_text_gets_no_diagnostic(string statement)
    {
        // Act
        var diagnostics = await RunInMethod(statement);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""var text = "a\r\nb";""")]
    [Arguments("""var text = "a\t\"b\"";""")]
    [Arguments("""var text = "a\u0041\nb";""")]
    [Arguments("""var text = "\"";""")]
    [Arguments("""var text = "\"\"";""")]
    [Arguments("""var text = "paths:\n  ";""")]
    [Arguments("""var text = "a \nb";""")]
    [Arguments("""var text = "\"a\" ";""")]
    public async Task a_string_that_a_raw_string_cannot_show_better_gets_no_diagnostic(string statement)
    {
        // Act
        var diagnostics = await RunInMethod(statement);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_raw_string_that_holds_csharp_source_gets_no_diagnostic()
    {
        // Act
        var diagnostics = await RunInMethod(""""
            var source = """
                var text = "say \"hi\" now\nand go";
                """;
            """");

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("Source.cs", "// <auto-generated/>\n")]
    [Arguments("Source.g.cs", "")]
    public async Task generated_code_gets_no_diagnostic(string path, string header)
    {
        // Act
        var diagnostics = await Run("""
            public static class Sample
            {
                public const string Text = "say \"hi\" now";
            }
            """, path, header);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""
        var text = "
        """)]
    [Arguments("""
        var text = @"
        """)]
    [Arguments("""
        var text = "say \"hi
        """)]
    [Arguments("""
        var text = $"a\nb
        """)]
    public async Task an_unterminated_string_gets_no_diagnostic_and_no_exception(string statement)
    {
        // Act
        var diagnostics = await Run($$"""
            public static class Sample
            {
                public static void Run()
                {
                    {{statement}}
                }
            }
            """, hasSyntaxError: true);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    private static string Text(Diagnostic diagnostic)
        => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    private static Task<ImmutableArray<Diagnostic>> RunInMethod(string statement)
        => Run($$"""
            public static class Sample
            {
                public static void Run(string id)
                {
                    {{statement}}
                }
            }
            """);

    private static async Task<ImmutableArray<Diagnostic>> Run(string source, string path = "Source.cs", string header = "", bool hasSyntaxError = false)
    {
        var compilation = CSharpCompilation.Create(
            "Features",
            [CSharpSyntaxTree.ParseText($"""
                {header}namespace App;

                {source}
                """, path: path)],
            PlatformReferences.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        if (hasSyntaxError)
            compilation.GetParseDiagnostics().ShouldNotBeEmpty();
        else
            compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        var exceptions = new List<Exception>();
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: (exception, _, _) => exceptions.Add(exception),
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false);
        var diagnostics = await compilation
            .WithAnalyzers([new RawStringAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current!.Execution.CancellationToken);

        exceptions.ShouldBeEmpty();
        return [.. diagnostics.OrderBy(d => d.Location.SourceSpan.Start)];
    }
}
