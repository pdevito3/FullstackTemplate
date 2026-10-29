namespace FullstackTemplate.UnitTests.Docs;

using System.Text.RegularExpressions;
using Shouldly;

/// <summary>
/// Each C# block of docs/backend/*.md comes from a file that the build compiles. The line before the block names the
/// file: <c>&lt;!-- code: path/from/the/root.cs --&gt;</c>. Each part of the block between two <c>// ...</c> lines
/// must be in the file as the same lines, in the same order. The indent of a line and a blank line do not count.
/// </summary>
public partial class DocCodeBlockTests
{
    [Test]
    public void a_block_with_no_source_a_missing_source_and_a_changed_line_are_listed()
    {
        // Arrange
        const string markdown = """
            # Doc

            ```csharp
            var a = 1;
            ```

            <!-- code: Server/Gone.cs -->
            ```csharp
            var b = 2;
            ```

            <!-- code: Server/Entity.cs -->
            ```csharp
            public int A { get; }
            // ...
            public int C { get; }
            ```

            <!-- code: Server/Entity.cs -->
            ```csharp
            public int A { get; }
            public int B { get; set; }
            ```
            """;
        var files = new Dictionary<string, string>
        {
            ["Server/Entity.cs"] = "class Entity\n{\n    public int A { get; }\n    public int B { get; }\n    public int C { get; }\n}\n",
        };

        // Act
        var faults = FaultsOf("docs/backend/x.md", markdown, path => files.GetValueOrDefault(path));

        // Assert
        faults.ShouldBe([
            "docs/backend/x.md line 3: the C# block has no source. Put <!-- code: path/to/File.cs --> on the line before it, and copy the lines from that file.",
            "docs/backend/x.md line 8: the C# block names Server/Gone.cs, but the file does not exist.",
            "docs/backend/x.md line 20: the C# block differs from Server/Entity.cs at 'public int B { get; set; }'. Copy the lines from the file again.",
        ]);
    }

    [Test]
    public void each_csharp_block_of_the_backend_docs_is_in_its_source_file()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var docs = Directory.GetFiles(Path.Combine(root, "docs", "backend"), "*.md").Order(StringComparer.Ordinal);

        // Act
        var faults = docs.SelectMany(doc => FaultsOf(
            Path.GetRelativePath(root, doc).Replace('\\', '/'),
            File.ReadAllText(doc),
            path => ReadSource(root, path))).ToList();

        // Assert
        faults.ShouldBeEmpty();
    }

    private static string? ReadSource(string root, string path)
    {
        var full = Path.GetFullPath(Path.Combine(root, path));
        var segments = path.Split('/');
        return full.StartsWith(root, StringComparison.Ordinal) && path.EndsWith(".cs", StringComparison.Ordinal)
            && !segments.Contains("bin") && !segments.Contains("obj") && File.Exists(full)
            ? File.ReadAllText(full)
            : null;
    }

    private static List<string> FaultsOf(string docPath, string markdown, Func<string, string?> readSource)
    {
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var faults = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!CSharpFence().IsMatch(lines[i]))
                continue;

            var open = i;
            var end = Array.FindIndex(lines, open + 1, l => l.TrimStart().StartsWith("```", StringComparison.Ordinal));
            if (end < 0)
                end = lines.Length;
            i = end;

            var lineNumber = open + 1;
            var before = Enumerable.Range(0, open).Reverse().Select(n => lines[n]).FirstOrDefault(l => l.Trim().Length > 0);
            if (before is null || SourceComment().Match(before) is not { Success: true } source)
            {
                faults.Add($"{docPath} line {lineNumber}: the C# block has no source. Put <!-- code: path/to/File.cs --> on the line before it, and copy the lines from that file.");
                continue;
            }

            var path = source.Groups[1].Value;
            if (readSource(path) is not { } text)
            {
                faults.Add($"{docPath} line {lineNumber}: the C# block names {path}, but the file does not exist.");
                continue;
            }

            if (FirstLineNotInSource(lines[(open + 1)..end], text) is { } missing)
                faults.Add($"{docPath} line {lineNumber}: the C# block differs from {path} at '{missing}'. Copy the lines from the file again.");
        }

        return faults;
    }

    // Each part between two "// ..." lines must be a run of lines of the source, after the end of the part before it.
    // Blank lines do not count.
    private static string? FirstLineNotInSource(string[] block, string source)
    {
        var sourceLines = source.ReplaceLineEndings("\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        var parts = new List<List<string>> { new() };
        foreach (var line in block.Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            if (line == "// ...")
                parts.Add([]);
            else
                parts[^1].Add(line);
        }

        var from = 0;
        foreach (var part in parts)
        {
            if (part.Count == 0)
                continue;

            var at = IndexOfRun(sourceLines, part, from);
            if (at < 0)
                return part[LongestMatchedPrefix(sourceLines, part, from)];
            from = at + part.Count;
        }

        return null;
    }

    private static int IndexOfRun(string[] source, List<string> run, int from)
    {
        for (var start = from; start + run.Count <= source.Length; start++)
        {
            if (run.Select((line, k) => source[start + k] == line).All(same => same))
                return start;
        }

        return -1;
    }

    private static int LongestMatchedPrefix(string[] source, List<string> run, int from)
    {
        var best = 0;
        for (var start = from; start < source.Length; start++)
        {
            var length = 0;
            while (length < run.Count && start + length < source.Length && source[start + length] == run[length])
                length++;
            best = Math.Max(best, length);
        }

        return Math.Min(best, run.Count - 1);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    [GeneratedRegex(@"^\s*```(?:csharp|cs|c#)\s*$")]
    private static partial Regex CSharpFence();

    [GeneratedRegex(@"^\s*<!--\s*code:\s*(\S+)\s*-->\s*$")]
    private static partial Regex SourceComment();
}
