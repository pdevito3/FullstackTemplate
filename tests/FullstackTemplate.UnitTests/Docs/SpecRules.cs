namespace FullstackTemplate.UnitTests.Docs;

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Reads the rule IDs of the specs in <c>docs/specs/</c> and the rule IDs that the tests name, and lists each fault
/// between them (see <c>docs/specs/TEMPLATE.md</c>, section "Rules"). A spec defines a rule in a row of a table whose
/// first column is <c>ID</c>. The first cell of the row is the ID, such as <c>| R-12 |</c>, or <c>| R-12 (later) |</c> for
/// a rule that no slice built yet. A .NET <c>[Test]</c> method with no <c>[Skip]</c> names a rule with
/// <c>[Property("Rule", "R-12")]</c>. A frontend <c>test</c> or <c>it</c> names it with <c>@R-12</c> in its title, its
/// tag, or its annotation.
/// </summary>
public static partial class SpecRules
{
    /// <summary>A rule of a spec. <paramref name="Later"/> is true when no slice built it yet.</summary>
    public sealed record Rule(string Id, string Spec, bool Later);

    /// <summary>A rule ID that a test names, and the file of the test.</summary>
    public sealed record Reference(string Id, string File);

    /// <summary>The rules of a spec, and each line of a rule table that does not give a rule ID.</summary>
    public sealed record Spec(IReadOnlyList<Rule> Rules, IReadOnlyList<string> Faults);

    public static Spec Read(string spec, string markdown)
    {
        var rules = new List<Rule>();
        var faults = new List<string>();
        string? fence = null;
        var inTable = false;
        var inRuleTable = false;
        var section = (RulesSection?)null;
        var lines = markdown.Split('\n');
        for (var number = 1; number <= lines.Length; number++)
        {
            var line = lines[number - 1].Trim();
            if (Fence().Match(line) is { Success: true } marker && (fence is null || marker.Value.StartsWith(fence, StringComparison.Ordinal)))
            {
                fence = fence is null ? marker.Value : null;
                inTable = false;
                continue;
            }

            if (fence is null && Heading().Match(line) is { Success: true } heading)
            {
                var level = heading.Groups["level"].Length;
                if (section is not null && level <= section.Level)
                {
                    faults.AddRange(section.Faults(spec));
                    section = null;
                }

                if (RulesHeading().IsMatch(heading.Groups["text"].Value))
                    section = new RulesSection(level);
                inTable = false;
                continue;
            }

            if (fence is null && section is not null && line.Length > 0)
            {
                section.HasText = true;
                section.IsNone = line is "None" or "None.";
                if (RuleInList().Match(line) is { Success: true } listed)
                    faults.Add($"Line {number} of {spec} has the rule {listed.Groups["id"].Value} in a list. Write each rule as a row of the table whose first column is ID.");
            }

            // A row of a GitHub table holds a '|', and the '|' at its start is optional
            if (fence is not null || !line.Contains('|', StringComparison.Ordinal))
            {
                inTable = false;
                continue;
            }

            var firstCell = (line.StartsWith('|') ? line[1..] : line).Split('|')[0].Trim();
            if (!inTable)
            {
                inTable = true;
                inRuleTable = firstCell.Trim('*', '`').Equals("ID", StringComparison.OrdinalIgnoreCase);
                if (section is not null && inRuleTable)
                    section.HasRuleTable = true;
                else if (section is not null)
                    faults.Add($"Line {number} of {spec} starts a table in the Rules section whose first column is '{firstCell}'. Name the first column ID.");
                continue;
            }

            if (!inRuleTable || SeparatorCell().IsMatch(firstCell))
                continue;

            if (RuleId().Match(firstCell) is { Success: true } id)
                rules.Add(new Rule(id.Groups["id"].Value, spec, id.Groups["later"].Success));
            else
                faults.Add($"Line {number} of {spec} has '{firstCell}' in the ID column. Write the ID as R-1, or as R-1 (later).");
        }

        if (section is not null)
            faults.AddRange(section.Faults(spec));

        return new Spec(rules, faults);
    }

    public static IEnumerable<Reference> NamedIn(string file, string source)
        => (file.EndsWith(".cs", StringComparison.Ordinal) ? DotnetRuleIds(source) : FrontendRuleIds(source))
            .Select(id => new Reference(id, file));

    public static IEnumerable<string> FaultsOf(IReadOnlyCollection<Rule> rules, IReadOnlyCollection<Reference> references)
    {
        foreach (var duplicate in rules.GroupBy(r => r.Id).Where(g => g.Count() > 1))
            yield return $"{duplicate.Key} is defined more than once, in {string.Join(" and ", duplicate.Select(r => r.Spec).Distinct())}. Give each rule its own ID.";

        var tested = references.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var rule in rules.DistinctBy(r => r.Id).OrderBy(r => r.Spec, StringComparer.Ordinal).ThenBy(r => r.Id.Length).ThenBy(r => r.Id, StringComparer.Ordinal))
        {
            if (!rule.Later && !tested.Contains(rule.Id))
                yield return $"{rule.Id} of {rule.Spec} has no test. Name it in the test that proves it, or mark it (later) in the spec if no slice built it yet.";
            if (rule.Later && tested.Contains(rule.Id))
                yield return $"{rule.Id} of {rule.Spec} has a test, so a slice built it. Remove (later) from its row.";
        }

        var defined = rules.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var reference in references.Where(r => !defined.Contains(r.Id)).OrderBy(r => r.File, StringComparer.Ordinal))
            yield return $"{reference.File} names the rule {reference.Id}, but no spec in docs/specs defines it. Fix the ID, or add the rule to the spec.";
    }

    // The section under a heading named Rules, down to the next heading of the same or a higher level
    private sealed class RulesSection(int level)
    {
        public int Level { get; } = level;

        public bool HasRuleTable { get; set; }

        public bool HasText { get; set; }

        public bool IsNone { get; set; }

        public IEnumerable<string> Faults(string spec)
        {
            if (!HasRuleTable && !(HasText && IsNone))
                yield return $"The Rules section of {spec} has no table whose first column is ID, so the check reads no rules from it. Write each rule as a row of that table, or write None.";
        }
    }

    private static IEnumerable<string> DotnetRuleIds(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var attributes = method.AttributeLists.SelectMany(list => list.Attributes).ToList();
            var skipped = attributes.Any(a => IsAttribute(a, "Skip"))
                || method.Ancestors().OfType<TypeDeclarationSyntax>().SelectMany(t => t.AttributeLists).SelectMany(list => list.Attributes).Any(a => IsAttribute(a, "Skip"));
            if (!attributes.Any(a => IsAttribute(a, "Test")) || skipped)
                continue;

            foreach (var property in attributes.Where(a => IsAttribute(a, "Property")))
            {
                if (property.ArgumentList?.Arguments is [{ Expression: LiteralExpressionSyntax { Token.ValueText: "Rule" } }, { Expression: LiteralExpressionSyntax id }]
                    && id.IsKind(SyntaxKind.StringLiteralExpression))
                    yield return id.Token.ValueText;
            }
        }
    }

    private static bool IsAttribute(AttributeSyntax attribute, string name)
        => attribute.Name.ToString().Replace("global::", "", StringComparison.Ordinal) is var written
            && (written == name || written == name + "Attribute" || written == "TUnit.Core." + name || written == "TUnit.Core." + name + "Attribute");

    // The title and the options of each test(...) and it(...) call. test.skip and test.fixme do not run, so they do not count.
    private static IEnumerable<string> FrontendRuleIds(string source)
    {
        var code = WithoutComments(source);
        foreach (Match call in FrontendTestCall().Matches(code))
        {
            var start = SkipSpace(code, call.Index + call.Length);
            if (start >= code.Length || code[start] is not ('\'' or '"' or '`'))
                continue;

            var end = EndOfString(code, start);
            var named = code[start..end];
            var next = SkipSpace(code, end);
            if (next < code.Length && code[next] == ',' && SkipSpace(code, next + 1) is var options && options < code.Length && code[options] == '{')
                named += code[options..EndOfObject(code, options)];

            foreach (Match id in FrontendReference().Matches(named))
                yield return id.Groups["id"].Value;
        }
    }

    // Each comment becomes spaces. A // or a /* in a string stays.
    private static string WithoutComments(string source)
    {
        var code = source.ToCharArray();
        for (var i = 0; i < code.Length; i++)
        {
            if (code[i] is '\'' or '"' or '`')
            {
                i = EndOfString(source, i) - 1;
            }
            else if (code[i] == '/' && i + 1 < code.Length && code[i + 1] is '/' or '*')
            {
                var end = code[i + 1] == '/' ? source.IndexOf('\n', i) : source.IndexOf("*/", i + 2, StringComparison.Ordinal) + 2;
                end = end <= 1 ? code.Length : end;
                Array.Fill(code, ' ', i, end - i);
                i = end - 1;
            }
        }

        return new string(code);
    }

    // The index after the quote that closes the string that starts at start
    private static int EndOfString(string code, int start)
    {
        for (var i = start + 1; i < code.Length; i++)
        {
            if (code[i] == '\\')
                i++;
            else if (code[i] == code[start])
                return i + 1;
        }

        return code.Length;
    }

    private static int EndOfObject(string code, int start)
    {
        var depth = 0;
        for (var i = start; i < code.Length; i++)
        {
            if (code[i] is '\'' or '"' or '`')
                i = EndOfString(code, i) - 1;
            else if (code[i] == '{')
                depth++;
            else if (code[i] == '}' && --depth == 0)
                return i + 1;
        }

        return code.Length;
    }

    private static int SkipSpace(string code, int index)
    {
        while (index < code.Length && char.IsWhiteSpace(code[index]))
            index++;

        return index;
    }

    [GeneratedRegex(@"^(?<level>#{1,6})\s+(?<text>.*?)\s*#*$")]
    private static partial Regex Heading();

    // "Rules" or "3. Rules"
    [GeneratedRegex(@"^(?:[0-9]+(?:\.[0-9]+)*\.?\s+)?Rules$", RegexOptions.IgnoreCase)]
    private static partial Regex RulesHeading();

    // "- R-7: ...", "1. **R-7** ..."
    [GeneratedRegex(@"^(?:[-*+]|[0-9]+[.)])\s+[*_`]*(?<id>[A-Z][A-Z0-9]*-[0-9]+)\b")]
    private static partial Regex RuleInList();

    [GeneratedRegex(@"(?<![\w.$])(?:test|it)(?:\.only)?\s*\(")]
    private static partial Regex FrontendTestCall();

    [GeneratedRegex(@"^(?<id>[A-Z][A-Z0-9]*-[0-9]+)(?<later>\s+\(later\))?$")]
    private static partial Regex RuleId();

    [GeneratedRegex(@"^:?-+:?$")]
    private static partial Regex SeparatorCell();

    // A code block opens with three or more backticks or tildes, and closes with at least as many of the same
    [GeneratedRegex(@"^(`{3,}|~{3,})")]
    private static partial Regex Fence();

    [GeneratedRegex(@"(?<![\w.])@(?<id>[A-Z][A-Z0-9]*-[0-9]+)\b")]
    private static partial Regex FrontendReference();
}
