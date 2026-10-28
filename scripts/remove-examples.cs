// Deletes the example code of the template. See docs/examples.md.
//
//   dotnet run scripts/remove-examples.cs
//
// A second run changes nothing.

using System.ComponentModel;
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

    app.FailIfTreeHasChanges();
    var removal = app.FindExamples();

    changing = true;
    app.Remove(removal);
    app.WriteModelSnapshot();
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
    private const string SnapshotMigration = "RemoveExamplesSnapshot";

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

    // The snapshot is generated, so EF writes it again for the model with no example entities. The script deletes the
    // migration that EF adds with it, so a new database never has the example tables. See docs/examples.md.
    public void WriteModelSnapshot()
    {
        var server = $"{Name}.Server";
        Run(root, "dotnet", "tool", "restore");
        Run(root, "dotnet", "tool", "run", "dotnet-ef", "migrations", "add", SnapshotMigration,
            "--project", server, "--startup-project", server, "--output-dir", "Databases/Migrations");

        var migrations = Path.Combine(root, server, "Databases", "Migrations");
        foreach (var file in Directory.GetFiles(migrations, $"*_{SnapshotMigration}.cs")
                     .Concat(Directory.GetFiles(migrations, $"*_{SnapshotMigration}.Designer.cs")))
            File.Delete(file);
    }

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

    private static void WriteText(string path, string text)
    {
        var hasBom = File.ReadAllBytes(path).AsSpan().StartsWith(Encoding.UTF8.Preamble);
        File.WriteAllBytes(path, [.. hasBom ? Encoding.UTF8.Preamble : [], .. Encoding.UTF8.GetBytes(text)]);
    }

    // git ls-files prints paths with forward slashes on every platform.
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
        var line = $"{command} {string.Join(' ', arguments)}";
        Console.WriteLine($"> {line}");
        // On Windows, pnpm is a .cmd file, and Process.Start finds only an .exe
        var startInfo = OperatingSystem.IsWindows() && command == "pnpm"
            ? new ProcessStartInfo("cmd.exe", ["/c", command, .. arguments])
            : new ProcessStartInfo(command, arguments);
        startInfo.WorkingDirectory = folder;
        try
        {
            using var process = Process.Start(startInfo) ?? throw new RemovalException($"The script could not start {command}.");
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new RemovalException($"'{line}' failed in {folder}.");
        }
        catch (Win32Exception exception)
        {
            throw new RemovalException($"The script could not start {command}: {exception.Message}");
        }
    }

    private const string MarkerStart = @"^[ \t]*(?:<!--|//|#|\{/\*)[ \t]*example:";

    [GeneratedRegex(@"(?:^[ \t]*\r?\n)?" + MarkerStart + @"start\b.*?" + MarkerStart + @"end\b[^\n]*(?:\n|\z)", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex ExampleSectionPattern();

    [GeneratedRegex(MarkerStart + @"(?:start|end)\b[^\n]*", RegexOptions.Multiline)]
    private static partial Regex MarkerLinePattern();
}

internal sealed class RemovalException(string message) : Exception(message);
