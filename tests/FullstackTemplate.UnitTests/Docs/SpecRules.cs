namespace FullstackTemplate.UnitTests.Docs;

using System.Text.RegularExpressions;

/// <summary>
/// Reads the rule IDs of the specs in <c>docs/specs/</c> and the rule IDs that the tests name, and lists each fault
/// between them (see <c>docs/specs/TEMPLATE.md</c>, section "Rules"). A spec defines a rule in a row of a table whose
/// first column is <c>ID</c>. The first cell of the row is the ID, such as <c>| R-12 |</c>, or <c>| R-12 (later) |</c> for
/// a rule that no slice built yet. A .NET test names a rule with <c>[Property("Rule", "R-12")]</c>, and a frontend test
/// with the tag <c>@R-12</c>.
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
                continue;
            }

            if (!inRuleTable || SeparatorCell().IsMatch(firstCell))
                continue;

            if (RuleId().Match(firstCell) is { Success: true } id)
                rules.Add(new Rule(id.Groups["id"].Value, spec, id.Groups["later"].Success));
            else
                faults.Add($"Line {number} of {spec} has '{firstCell}' in the ID column. Write the ID as R-1, or as R-1 (later).");
        }

        return new Spec(rules, faults);
    }

    public static IEnumerable<Reference> NamedIn(string file, string source)
        => (file.EndsWith(".cs", StringComparison.Ordinal) ? DotnetReference() : FrontendReference())
            .Matches(source)
            .Select(m => new Reference(m.Groups["id"].Value, file));

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

    [GeneratedRegex(@"^(?<id>[A-Z][A-Z0-9]*-[0-9]+)(?<later>\s+\(later\))?$")]
    private static partial Regex RuleId();

    [GeneratedRegex(@"^:?-+:?$")]
    private static partial Regex SeparatorCell();

    // A code block opens with three or more backticks or tildes, and closes with at least as many of the same
    [GeneratedRegex(@"^(`{3,}|~{3,})")]
    private static partial Regex Fence();

    [GeneratedRegex(@"(?<![\w.])(?:TUnit\.Core\.)?Property(?:Attribute)?\(\s*""Rule""\s*,\s*""(?<id>[^""]+)""\s*\)")]
    private static partial Regex DotnetReference();

    [GeneratedRegex(@"(?<![\w.])@(?<id>[A-Z][A-Z0-9]*-[0-9]+)\b")]
    private static partial Regex FrontendReference();
}
