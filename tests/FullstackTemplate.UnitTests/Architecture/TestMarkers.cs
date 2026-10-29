namespace FullstackTemplate.UnitTests.Architecture;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Reads the <c>// Arrange</c>, <c>// Act</c>, and <c>// Assert</c> markers of each <c>[Test]</c> method in a source
/// file. A test passes when its body is a block, the first marker comes before the first statement, and the markers
/// give Arrange, Act, and Assert in this order. Arrange is optional, and one line can join parts, as in
/// <c>// Act &amp; Assert</c>.
/// </summary>
public static class TestMarkers
{
    private enum Part { Arrange, Act, Assert }

    private static readonly Dictionary<string, Part> PartNames =
        Enum.GetValues<Part>().ToDictionary(part => part.ToString(), StringComparer.OrdinalIgnoreCase);

    /// <summary>A test that breaks the rule, and why.</summary>
    public sealed record Violation(string Test, string Reason);

    public static IEnumerable<Violation> Of(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(IsTest))
        {
            var test = $"{method.Ancestors().OfType<TypeDeclarationSyntax>().First().Identifier.Text}.{method.Identifier.Text}";
            if (ReasonOf(method) is { } reason)
                yield return new Violation(test, reason);
        }
    }

    private static string? ReasonOf(MethodDeclarationSyntax method)
    {
        if (method.Body is not { } body)
            return "has an expression body, so it has no markers";

        var markers = body.DescendantTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) && t.SpanStart > body.OpenBraceToken.SpanStart && !InNestedFunction(t, body))
            .Select(t => (t.SpanStart, Parts: PartsOf(t.ToString())))
            .Where(m => m.Parts.Count > 0)
            .ToList();
        if (markers.Count == 0)
            return "has no markers";

        if (body.Statements.FirstOrDefault() is { } first && first.SpanStart < markers[0].SpanStart)
            return "has code before its first marker";

        var parts = markers.SelectMany(m => m.Parts).ToList();
        if (!parts.SequenceEqual(parts.Distinct().Order()))
            return $"has the markers {string.Join(", ", parts)}, not in the order Arrange, Act, Assert";
        if (!parts.Contains(Part.Act))
            return "has no // Act";
        if (!parts.Contains(Part.Assert))
            return "has no // Assert";

        return null;
    }

    // "// Act & Assert" and "// Arrange and act - the user" give two parts. "// Asserts that ..." is not a marker.
    private static List<Part> PartsOf(string comment)
    {
        var text = comment.TrimStart('/').Trim();
        var head = text.Split([" - ", ":"], 2, StringSplitOptions.None)[0];
        var words = head.Split([" & ", " and "], StringSplitOptions.TrimEntries);
        if (words[0].Length == 0 || !char.IsUpper(words[0][0]))
            return [];

        var parts = new List<Part>();
        foreach (var word in words)
        {
            if (!PartNames.TryGetValue(word, out var part))
                return [];
            parts.Add(part);
        }

        return parts;
    }

    // A lambda or a local function in the test has its own comments, which do not mark the parts of the test
    private static bool InNestedFunction(SyntaxTrivia trivia, BlockSyntax body)
        => trivia.Token.Parent?.AncestorsAndSelf()
            .TakeWhile(node => node != body)
            .Any(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax) == true;

    private static bool IsTest(MethodDeclarationSyntax method)
        => method.AttributeLists.SelectMany(list => list.Attributes)
            .Select(a => a.Name.ToString().Replace("global::", "", StringComparison.Ordinal))
            .Any(name => name is "Test" or "TestAttribute" or "TUnit.Core.Test" or "TUnit.Core.TestAttribute");
}
