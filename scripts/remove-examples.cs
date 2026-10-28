// Deletes the example code of the template from an app. See docs/examples.md.
//
//   dotnet run scripts/remove-examples.cs
//
// The script deletes each folder named Examples, examples, or (examples), and each section between example:start and
// example:end comments. Then it writes the API contract and the route tree of the SPA again, because the examples
// are in them. A second run changes nothing.

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

if (args.Length > 0)
{
    Console.Error.WriteLine("Usage: dotnet run scripts/remove-examples.cs");
    return 1;
}

var changing = false;
try
{
    var root = Path.GetFullPath(Path.Combine((string)AppContext.GetData("EntryPointFileDirectoryPath")!, ".."));
    var app = new AppRepo(root);

    // Find all the work first, so that a marker with no end stops the script before it changes a file
    var removal = app.FindExamples();
    if (removal.IsDue)
    {
        app.FailIfTreeHasChanges();
        changing = true;
        app.Remove(removal);
    }

    // Always, so that a second run finishes the work of a first run that stopped before the end
    changing = true;
    app.WriteGeneratedFiles();

    Console.WriteLine(removal.IsDue
        ? $"Deleted {removal.Folders.Count} example folders and the example sections of {removal.Sections.Count} files."
        : "The app has no example code.");
    Console.WriteLine($"Next: dotnet build {app.Name}.slnx");
    return 0;
}
catch (RemovalException exception)
{
    Console.Error.WriteLine(exception.Message);
    if (changing)
        Console.Error.WriteLine("To undo the changes, run: git reset --hard && git clean -fd");
    return 1;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(changing
        ? $"The script stopped before the end: {exception.Message}\nTo undo the changes, run: git reset --hard && git clean -fd"
        : $"The script changed no files: {exception.Message}");
    return 1;
}

internal sealed record Removal(IReadOnlyList<string> Folders, IReadOnlyDictionary<string, string> Sections)
{
    public bool IsDue => Folders.Count > 0 || Sections.Count > 0;
}

internal sealed partial class AppRepo(string root)
{
    private const string ThisScript = "scripts/remove-examples.cs";

    // The names of a folder that holds only example code. The SPA uses kebab case, and (examples) is a TanStack
    // Router route group, which adds no segment to the URL.
    private static readonly HashSet<string> ExampleFolderNames = new(StringComparer.Ordinal) { "Examples", "examples", "(examples)" };

    public string Name => Path.GetFileNameWithoutExtension(
        Directory.GetFiles(root, "*.slnx") is [var solution]
            ? solution
            : throw new RemovalException($"The folder {root} must hold exactly one .slnx file."));

    private string Frontend => Path.Combine(root, "frontend");

    public Removal FindExamples()
    {
        var files = GitFiles().Where(file => file != ThisScript && File.Exists(Path.Combine(root, file))).ToList();

        var folders = files
            .Select(ExampleFolderOf)
            .OfType<string>()
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        var sections = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files.Where(file => ExampleFolderOf(file) is null))
        {
            if (ReadText(Path.Combine(root, file)) is not { } text || !text.Contains("example:", StringComparison.Ordinal))
                continue;

            var removed = ExampleSectionPattern().Replace(text, "");
            if (MarkerLinePattern().Match(removed) is { Success: true } marker)
            {
                var line = removed[..marker.Index].Count(c => c == '\n') + 1;
                throw new RemovalException($"{file} has an example marker with no partner near line {line}: {marker.Value.Trim()}");
            }
            if (removed != text)
                sections[file] = removed;
        }

        return new Removal(folders, sections);
    }

    public void FailIfTreeHasChanges()
    {
        if (Git("status", "--porcelain").Length > 0)
            throw new RemovalException("The git tree has changes. Commit or stash them, then run the script again.");
    }

    public void Remove(Removal removal)
    {
        foreach (var folder in removal.Folders)
            Directory.Delete(Path.Combine(root, folder), recursive: true);
        foreach (var (file, text) in removal.Sections)
            WriteText(Path.Combine(root, file), text);
    }

    // The same steps as the api-contract job of CI: delete the contract, so that the files of a removed endpoint go,
    // then build the Server, which writes the OpenAPI document, and generate the client. pnpm build writes the route
    // tree, and its tsc step fails on a file that still imports a deleted example.
    public void WriteGeneratedFiles()
    {
        foreach (var document in Directory.GetFiles(Path.Combine(root, $"{Name}.Server", "openapi"), "*.json"))
            File.Delete(document);
        foreach (var generated in Directory.GetDirectories(Path.Combine(Frontend, "src", "api"), "generated*"))
            Directory.Delete(generated, recursive: true);

        Run(root, "dotnet", "build", Path.Combine($"{Name}.Server", $"{Name}.Server.csproj"), "--no-incremental", "--configuration", "Debug");
        Run(Frontend, "pnpm", "install", "--frozen-lockfile");
        Run(Frontend, "pnpm", "generate:api");
        Run(Frontend, "pnpm", "build");
    }

    // The outermost folder of the path with an example name, or null
    private static string? ExampleFolderOf(string file)
    {
        var parts = file.Split('/');
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (ExampleFolderNames.Contains(parts[i]))
                return string.Join('/', parts[..(i + 1)]);
        }
        return null;
    }

    // Skips symlinks and binary files
    private static string? ReadText(string path)
    {
        if (new FileInfo(path).LinkTarget is not null)
            return null;

        var bytes = File.ReadAllBytes(path);
        if (bytes.Contains((byte)0))
            return null;

        var preamble = Encoding.UTF8.Preamble;
        return Encoding.UTF8.GetString(bytes.AsSpan().StartsWith(preamble) ? bytes[preamble.Length..] : bytes);
    }

    // Keeps a UTF-8 byte order mark
    private static void WriteText(string path, string text)
    {
        var hasBom = File.ReadAllBytes(path).AsSpan().StartsWith(Encoding.UTF8.Preamble);
        File.WriteAllBytes(path, [.. hasBom ? Encoding.UTF8.Preamble : [], .. Encoding.UTF8.GetBytes(text)]);
    }

    // The files that git tracks, and the new files that git does not ignore. Paths use forward slashes.
    private List<string> GitFiles() => Git("ls-files", "-z", "--cached", "--others", "--exclude-standard")
        .Split('\0', StringSplitOptions.RemoveEmptyEntries)
        .Distinct()
        .ToList();

    private string Git(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
        };
        using var git = Process.Start(startInfo) ?? throw new RemovalException("The script could not start git.");
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0)
            throw new RemovalException($"git {arguments[0]} failed in {root}. Run the script in a git clone of the app.");

        return output;
    }

    private static void Run(string folder, string command, params string[] arguments)
    {
        Console.WriteLine($"> {command} {string.Join(' ', arguments)}");
        using var process = Process.Start(new ProcessStartInfo(command, arguments) { WorkingDirectory = folder })
            ?? throw new RemovalException($"The script could not start {command}.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new RemovalException($"'{command} {string.Join(' ', arguments)}' failed in {folder}. Correct the cause, then run the script again.");
    }

    // The comment can be <!-- -->, //, #, or {/* */}, so that a section works in Markdown, C#, TypeScript, YAML, and
    // JSX. The blank line before the section goes too, so that no double blank line stays.
    [GeneratedRegex(@"(?:^[ \t]*\r?\n)?^[ \t]*(?:<!--|//|#|\{/\*) example:start\b.*?^[ \t]*(?:<!--|//|#|\{/\*) example:end\b[^\n]*\n", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex ExampleSectionPattern();

    [GeneratedRegex(@"^[ \t]*(?:<!--|//|#|\{/\*) example:(?:start|end)\b[^\n]*", RegexOptions.Multiline)]
    private static partial Regex MarkerLinePattern();
}

internal sealed class RemovalException(string message) : Exception(message);
