// Rewrites each string that FSTSTR001 refuses as a raw string literal. See "Strings" in docs/backend/code-style.md.
// Run it in the root of the app:
//
//   dotnet run scripts/raw-strings.cs
//   dotnet run scripts/raw-strings.cs -- --check <Commit>
//
// The rewrite reads each C# file that git knows, except the EF migrations, which the analyzer does not read. Before it
// writes a file, it makes sure that each string of the file keeps its value and that the other tokens stay the same.
// --check makes the same comparison between each C# file that changed since <Commit> and its text at <Commit>, and it
// reports a file that was added, deleted, or renamed. A second run of the rewrite changes nothing. The script does not
// write a file that is not UTF-8.

// Nothing publishes this script, so it needs no native AOT packages.
#:property PublishAot=false

#:project ../FullstackTemplate.SourceGenerators/FullstackTemplate.SourceGenerators.csproj
#:package Microsoft.CodeAnalysis.CSharp

using System.Diagnostics;
using System.Text;
using System.Text.Unicode;
using FullstackTemplate.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

const string Usage = """
    Usage: dotnet run scripts/raw-strings.cs
           dotnet run scripts/raw-strings.cs -- --check <Commit>
    """;

var root = Path.GetFullPath(Path.Combine((string)AppContext.GetData("EntryPointFileDirectoryPath")!, ".."));
var repo = new SourceRepo(root);
try
{
    switch (args)
    {
        case []:
            return await repo.Rewrite();
        case ["--check", var commit]:
            return repo.Check(commit);
        default:
            Console.Error.WriteLine(Usage);
            return 1;
    }
}
catch (ScriptException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

internal sealed class SourceRepo(string root)
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public async Task<int> Rewrite()
    {
        var rewrites = new List<(string File, string Text, bool Bom, int Sites)>();
        var left = new List<string>();
        foreach (var file in SourceFiles())
        {
            var path = Path.Combine(root, file);
            var bytes = File.ReadAllBytes(path);
            var bom = bytes is [0xEF, 0xBB, 0xBF, ..];
            var before = Utf8NoBom.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            var (after, sites, unchanged) = await RawStrings.Rewrite(before, file);
            left.AddRange(unchanged);
            if (sites == 0)
                continue;
            if (!Utf8.IsValid(bytes))
            {
                left.Add($"{file}: the file is not UTF-8");
                continue;
            }

            if (ValueCheck.Difference(before, after) is { } difference)
                throw new ScriptException($"The script changed no files. The rewrite of {file} changes the code: {difference}");
            rewrites.Add((file, after, bom, sites));
        }

        foreach (var (file, text, bom, sites) in rewrites)
        {
            File.WriteAllText(Path.Combine(root, file), text, bom ? Utf8WithBom : Utf8NoBom);
            Console.WriteLine($"{file}: {sites}");
        }

        Console.WriteLine($"Strings rewritten: {rewrites.Sum(r => r.Sites)}. Files changed: {rewrites.Count}. Each string keeps its value.");
        if (left.Count == 0)
            return 0;

        Console.Error.WriteLine("The script cannot rewrite these strings. Rewrite them by hand:");
        foreach (var site in left)
            Console.Error.WriteLine($"  {site}");
        return 1;
    }

    public int Check(string commit)
    {
        var entries = Git("diff", "--name-status", "--no-renames", "-z", commit, "--", "*.cs")
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Chunk(2)
            .ToList();
        var differences = 0;
        foreach (var entry in entries)
        {
            var (status, file) = (entry[0], entry[1]);
            var path = Path.Combine(root, file);
            var difference = status != "M"
                ? $"the file has the git status {status}"
                : !Utf8.IsValid(File.ReadAllBytes(path))
                    ? "the file is not UTF-8"
                    : ValueCheck.Difference(Git("show", $"{commit}:{file}").TrimStart('\uFEFF'), File.ReadAllText(path));
            if (difference is null)
                continue;
            Console.WriteLine($"{file}: {difference}");
            differences++;
        }

        Console.WriteLine($"Changed C# files compared with {commit}: {entries.Count}. Files with a difference that is not the form of a string: {differences}.");
        return differences == 0 ? 0 : 1;
    }

    private IEnumerable<string> SourceFiles() => Git("ls-files", "-z", "*.cs")
        .Split('\0', StringSplitOptions.RemoveEmptyEntries)
        .Where(file => !file.Contains("/Migrations/", StringComparison.Ordinal) && File.Exists(Path.Combine(root, file)));

    private string Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = StrictUtf8,
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new ScriptException("Could not start git.");
        var error = process.StandardError.ReadToEndAsync();
        string output;
        try
        {
            output = process.StandardOutput.ReadToEnd();
        }
        catch (DecoderFallbackException)
        {
            throw new ScriptException($"The output of git {string.Join(' ', arguments)} is not UTF-8.");
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new ScriptException($"git {string.Join(' ', arguments)} failed: {error.Result.Trim()}");
        return output;
    }
}

internal static class RawStrings
{
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.Preview)
        .WithFeatures([new("FileBasedProgram", "true")]);

    /// <summary>
    /// Rewrites each FSTSTR001 string of a file. A string can hold another string in a hole, so each pass rewrites only
    /// the strings that hold no other one, and the next pass gets the strings around them.
    /// </summary>
    public static async Task<(string Text, int Sites, IReadOnlyList<string> Unchanged)> Rewrite(string text, string file)
    {
        var total = 0;
        while (true)
        {
            var sites = await Sites(text, file);
            var free = sites.Where(site => !sites.Any(other => other != site && site.Span.Contains(other.Span))).ToList();
            var replacements = free
                .Select(site => (Site: site, Text: RawForm(site, text)))
                .Where(r => r.Text is not null)
                .OrderByDescending(r => r.Site.SpanStart)
                .ToList();
            if (replacements.Count == 0)
            {
                var unchanged = sites
                    .Select(site => $"{file}:{site.GetLocation().GetLineSpan().StartLinePosition.Line + 1}")
                    .ToList();
                return (text, total, unchanged);
            }

            var next = new StringBuilder(text);
            foreach (var (site, raw) in replacements)
                next.Remove(site.SpanStart, site.Span.Length).Insert(site.SpanStart, raw);
            text = next.ToString();
            total += replacements.Count;
        }
    }

    public static CSharpSyntaxTree Parse(string text, string file = "") => (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(text, ParseOptions, file);

    private static async Task<List<ExpressionSyntax>> Sites(string text, string file)
    {
        var tree = Parse(text, file);
        var compilation = CSharpCompilation.Create("Sites", [tree]);
        var diagnostics = await compilation.WithAnalyzers([new RawStringAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        var node = tree.GetRoot();
        return [.. diagnostics
            .Select(d => node.FindNode(d.Location.SourceSpan, getInnermostNodeForTie: true))
            .Select(found => found.AncestorsAndSelf().OfType<ExpressionSyntax>()
                .First(e => e is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax))];
    }

    /// <summary>
    /// The raw string literal with the value of the string, or null when the script cannot write it: a hole that spans
    /// lines, a format clause with an escape, or a line end in the text that is not the line end of the file.
    /// </summary>
    private static string? RawForm(ExpressionSyntax site, string text)
    {
        var lineStart = text.LastIndexOf('\n', Math.Max(site.SpanStart - 1, 0)) + 1;
        var lineIndent = new string([.. text[lineStart..].TakeWhile(c => c is ' ' or '\t')]);
        var indent = lineIndent + (lineIndent.Contains('\t', StringComparison.Ordinal) ? "\t" : "    ");
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        // A text part holds its value, and a hole holds its source between the braces
        var parts = new List<(bool IsHole, string Text)>();
        var suffix = "";
        if (site is LiteralExpressionSyntax literal)
        {
            parts.Add((false, literal.Token.ValueText));
            if (literal.Token.IsKind(SyntaxKind.Utf8StringLiteralToken))
                suffix = literal.Token.Text[^2..];
        }
        else
        {
            foreach (var content in ((InterpolatedStringExpressionSyntax)site).Contents)
            {
                if (content is InterpolatedStringTextSyntax part)
                {
                    parts.Add((false, ValueCheck.TextValue(part)));
                    continue;
                }

                var hole = (InterpolationSyntax)content;
                if (hole.FormatClause is { } format && format.FormatStringToken.Text != format.FormatStringToken.ValueText)
                    return null;
                var source = hole.ToString()[1..^1];
                if (source.Contains('\n', StringComparison.Ordinal))
                    return null;
                parts.Add((true, source));
            }
        }

        var texts = string.Concat(parts.Where(p => !p.IsHole).Select(p => p.Text));
        var otherEnds = texts.Replace(newLine, "", StringComparison.Ordinal);
        if (otherEnds.Contains('\r', StringComparison.Ordinal) || otherEnds.Contains('\n', StringComparison.Ordinal))
            return null;
        var quotes = new string('"', Math.Max(3, LongestRun(texts, '"') + 1));
        var dollars = site is InterpolatedStringExpressionSyntax
            ? Math.Max(1, Math.Max(LongestRun(texts, '{'), LongestRun(texts, '}')) + 1)
            : 0;
        var open = new string('{', dollars);
        var close = new string('}', dollars);
        var body = string.Concat(parts.Select(p => p.IsHole ? open + p.Text + close : p.Text));
        var start = new string('$', dollars) + quotes;

        // A raw string on one line cannot start or end with a quote or hold a new line
        var (first, last) = (parts[0], parts[^1]);
        var ownLines = texts.Contains('\n', StringComparison.Ordinal)
            || (!first.IsHole && first.Text.StartsWith('"'))
            || (!last.IsHole && last.Text.EndsWith('"'));
        if (!ownLines)
            return start + body + quotes + suffix;

        var raw = new StringBuilder(start).Append(newLine);
        foreach (var line in body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            raw.Append(line.Length == 0 ? "" : indent + line).Append(newLine);
        return raw.Append(indent).Append(quotes).Append(suffix).ToString();
    }

    private static int LongestRun(string text, char c)
    {
        int best = 0, run = 0;
        foreach (var ch in text)
        {
            run = ch == c ? run + 1 : 0;
            best = Math.Max(best, run);
        }

        return best;
    }
}

/// <summary>
/// Compares two texts of a C# file as a list of tokens, with each string as its value. A change of the form of a string
/// compares equal, and a change of another token does not. Comments, white space, and disabled code do not count.
/// </summary>
internal static class ValueCheck
{
    public static string? Difference(string before, string after)
    {
        var newTree = RawStrings.Parse(after);
        if (newTree.GetDiagnostics().FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error) is { } error)
            return $"the new text does not parse: {error}";

        var oldItems = Items(RawStrings.Parse(before).GetRoot()).ToList();
        var newItems = Items(newTree.GetRoot()).ToList();
        for (var i = 0; i < Math.Max(oldItems.Count, newItems.Count); i++)
        {
            var old = i < oldItems.Count ? oldItems[i] : null;
            var @new = i < newItems.Count ? newItems[i] : null;
            if (old?.Value != @new?.Value)
                return $"line {old?.Line} had {old?.Value ?? "<end>"}, and line {@new?.Line} has {@new?.Value ?? "<end>"}";
        }

        return null;
    }

    // Roslyn keeps {{ and }} in the ValueText of a regular or verbatim interpolated string, but the program reads { and }
    public static string TextValue(InterpolatedStringTextSyntax text)
    {
        var start = ((InterpolatedStringExpressionSyntax)text.Parent!).StringStartToken;
        var value = text.TextToken.ValueText;
        return start.IsKind(SyntaxKind.InterpolatedStringStartToken) || start.IsKind(SyntaxKind.InterpolatedVerbatimStringStartToken)
            ? value.Replace("{{", "{", StringComparison.Ordinal).Replace("}}", "}", StringComparison.Ordinal)
            : value;
    }

    private static IEnumerable<Item> Items(SyntaxNode node)
    {
        if (node is InterpolatedStringExpressionSyntax interpolated)
        {
            yield return InterpolatedItem(interpolated);
            yield break;
        }

        foreach (var child in node.ChildNodesAndTokens())
        {
            if (child.AsNode() is { } inner)
            {
                foreach (var item in Items(inner))
                    yield return item;
            }
            else
                yield return TokenItem(child.AsToken());
        }
    }

    private static Item InterpolatedItem(InterpolatedStringExpressionSyntax interpolated)
    {
        var value = new StringBuilder("ISTR(");
        var text = new StringBuilder();
        foreach (var content in interpolated.Contents)
        {
            if (content is InterpolatedStringTextSyntax part)
            {
                text.Append(TextValue(part));
                continue;
            }

            var hole = (InterpolationSyntax)content;
            value.Append("T:").Append(Show(text.ToString())).Append("|H:").Append(Tokens(hole.Expression));
            text.Clear();
            if (hole.AlignmentClause is { } alignment)
                value.Append(',').Append(Tokens(alignment.Value));
            if (hole.FormatClause is { } format)
                value.Append(':').Append(format.FormatStringToken.ValueText);
            value.Append('|');
        }

        value.Append("T:").Append(Show(text.ToString())).Append(')');
        return new Item(value.ToString(), Line(interpolated.GetLocation()));
    }

    private static Item TokenItem(SyntaxToken token)
    {
        var line = Line(token.GetLocation());
        return token.Kind() switch
        {
            SyntaxKind.StringLiteralToken or SyntaxKind.SingleLineRawStringLiteralToken or SyntaxKind.MultiLineRawStringLiteralToken
                => new Item("STR:" + Show(token.ValueText), line),
            SyntaxKind.Utf8StringLiteralToken or SyntaxKind.Utf8SingleLineRawStringLiteralToken or SyntaxKind.Utf8MultiLineRawStringLiteralToken
                => new Item("U8STR:" + Show(token.ValueText), line),
            _ => new Item(token.Kind() + ":" + token.Text, line),
        };
    }

    private static string Tokens(SyntaxNode node) => string.Join(" ", Items(node).Select(item => item.Value));

    private static int Line(Location location) => location.GetLineSpan().StartLinePosition.Line + 1;

    private static string Show(string value) => value
        .Replace("\\", @"\\", StringComparison.Ordinal)
        .Replace("\r", @"\r", StringComparison.Ordinal)
        .Replace("\n", @"\n", StringComparison.Ordinal);

    private sealed record Item(string Value, int Line);
}

internal sealed class ScriptException(string message) : Exception(message);
