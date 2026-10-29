namespace FullstackTemplate.UnitTests.Docs;

using System.Text.RegularExpressions;
using Shouldly;

public partial class DocLinkTests
{
    private static readonly HashSet<string> SkippedFolders =
    [
        "bin", "obj", "node_modules", ".git", "dist", "test-results", "playwright-report", "TestResults",
        ".claude", "artifacts", ".audit",
    ];

    [Test]
    public void a_line_number_link_and_a_link_to_a_missing_file_are_listed()
    {
        // Arrange
        const string markdown = """
            See [the handler](../../FullstackTemplate.Server/Program.cs#L12) and [the rules](endpoints.md#the-rules).
            See [a gone file](gone.md) and [a route](<../../frontend/src/routes/(app)/a.tsx#L4>) and [the site](https://aspire.dev) and [mail](mailto:a@b.c) and [here](#top).
            ```
            [in a code block](nothing.md#L3)
            ```
            """;
        string[] existing = ["FullstackTemplate.Server/Program.cs", "docs/backend/endpoints.md", "frontend/src/routes/(app)/a.tsx"];

        // Act
        var faults = FaultsOf("docs/backend/features.md", markdown, existing.Contains);

        // Assert
        faults.ShouldBe([
            "docs/backend/features.md links to ../../FullstackTemplate.Server/Program.cs#L12 with a line number. Link to the file, and name the member in the text.",
            "docs/backend/features.md links to gone.md, but docs/backend/gone.md does not exist.",
            "docs/backend/features.md links to ../../frontend/src/routes/(app)/a.tsx#L4 with a line number. Link to the file, and name the member in the text.",
        ]);
    }

    [Test]
    public void each_doc_link_names_a_file_that_exists_and_no_line()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var docs = DocsUnder(root).Select(file => Relative(root, file));

        // Act
        var faults = docs.SelectMany(doc => FaultsOf(doc, File.ReadAllText(Path.Combine(root, doc)), path => Exists(root, path)));

        // Assert
        faults.ShouldBeEmpty();
    }

    private static IEnumerable<string> FaultsOf(string doc, string markdown, Func<string, bool> exists)
    {
        var folder = Path.GetDirectoryName(doc)!.Replace('\\', '/');
        foreach (Match link in Link().Matches(InlineCode().Replace(CodeBlock().Replace(markdown, ""), "")))
        {
            var target = link.Groups["target"].Value;
            if (target.StartsWith('#') || Scheme().IsMatch(target))
                continue;

            if (LineAnchor().IsMatch(target))
                yield return $"{doc} links to {target} with a line number. Link to the file, and name the member in the text.";

            var path = Normalize(folder, target.Split('#')[0]);
            if (!exists(path))
                yield return $"{doc} links to {target.Split('#')[0]}, but {path} does not exist.";
        }
    }

    private static string Normalize(string folder, string target)
    {
        var parts = new List<string>();
        foreach (var part in (folder.Length == 0 ? target : $"{folder}/{target}").Split('/'))
        {
            if (part is "." or "")
                continue;
            if (part == ".." && parts.Count > 0)
                parts.RemoveAt(parts.Count - 1);
            else
                parts.Add(Uri.UnescapeDataString(part));
        }

        return string.Join('/', parts);
    }

    private static IEnumerable<string> DocsUnder(string folder) =>
        Directory.EnumerateFiles(folder, "*.md").Concat(Directory.EnumerateDirectories(folder)
            .Where(child => !SkippedFolders.Contains(Path.GetFileName(child)))
            .SelectMany(DocsUnder));

    private static bool Exists(string root, string path) =>
        File.Exists(Path.Combine(root, path)) || Directory.Exists(Path.Combine(root, path));

    private static string Relative(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not find FullstackTemplate.slnx above the test output directory.");
    }

    [GeneratedRegex(@"\[[^\]]*\]\((?:<(?<target>[^>]+)>|(?<target>[^)\s]+))(?:\s+""[^""]*"")?\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"^\s*(```|~~~).*?^\s*\1", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex CodeBlock();

    [GeneratedRegex(@"`[^`\n]*`")]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.-]*:")]
    private static partial Regex Scheme();

    [GeneratedRegex(@"#L\d+")]
    private static partial Regex LineAnchor();
}
