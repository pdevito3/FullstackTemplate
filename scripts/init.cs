// Turns a clone of FullstackTemplate into a new app. See docs/adr/0007-regular-repo-instead-of-template.md.
//
//   dotnet run scripts/init.cs --name Acme.Shop --auth-provider Keycloak
//
// A second run with the same arguments changes nothing.

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

const string Usage = """
    Usage: dotnet run scripts/init.cs [--name <Name>] [--auth-provider <Provider>]

      --name <Name>               Rename FullstackTemplate to <Name> in file names, folder names, and file contents.
                                  <Name> is a C# namespace, for example Acme.Shop.
      --auth-provider <Provider>  Set the default AuthProvider of the AppHost. <Provider> is the name of a folder
                                  in <Name>.AppHost/AuthProviders/, for example Keycloak.

    The script changes only a clean git tree, so that `git reset --hard && git clean -fd` undoes a failed run.
    """;

string? name = null;
string? authProvider = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--name" when i + 1 < args.Length:
            name = args[++i];
            break;
        case "--auth-provider" when i + 1 < args.Length:
            authProvider = args[++i];
            break;
        default:
            Console.Error.WriteLine($"The argument '{args[i]}' is not known or has no value.");
            Console.Error.WriteLine(Usage);
            return 1;
    }
}

if (name is null && authProvider is null)
{
    Console.Error.WriteLine(Usage);
    return 1;
}

// The script writes files only after the clean-tree check, so only then can git undo a failed run
var changing = false;
try
{
    var root = Path.GetFullPath(Path.Combine((string)AppContext.GetData("EntryPointFileDirectoryPath")!, ".."));
    var app = new AppRepo(root);

    // Check the arguments first. Then require a clean tree only if a change is due, so that a second run with the
    // same arguments passes and changes nothing.
    var renameDue = name is not null && app.RenameIsDue(name);
    var provider = authProvider is null ? null : app.KnownProvider(authProvider);
    var providerDue = provider is not null && provider != app.AuthProvider;
    if (renameDue || providerDue)
    {
        app.FailIfTreeHasChanges();
        changing = true;
    }
    if (renameDue)
        app.Rename(name!);
    if (providerDue)
        app.SetAuthProvider(provider!);

    Console.WriteLine($"The app is {app.Name}. The default AuthProvider is {app.AuthProvider}.");
    Console.WriteLine($"Next: dotnet build {app.Name}.slnx");
    return 0;
}
catch (InitException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    if (!changing)
    {
        Console.Error.WriteLine($"The script changed no files: {exception.Message}");
        return 1;
    }

    Console.Error.WriteLine($"The script stopped before the end: {exception.Message}");
    Console.Error.WriteLine("To undo the changes, run: git reset --hard && git clean -fd");
    return 1;
}

internal sealed partial class AppRepo(string root)
{
    private const string TemplateName = "FullstackTemplate";
    private const string TemplateUserSecretsId = "4c914da6-509c-4fa2-ab1a-eb4125a15857";

    // This script holds the template name, and it must find it again on a second run, so the rename skips it.
    private const string ThisScript = "scripts/init.cs";

    // The CI job that runs this script. It can run only before the rename.
    private const string InitWorkflow = ".github/workflows/init.yml";

    // The README section that tells how to run this script. A renamed app has no use for it.
    private const string Readme = "README.md";

    public string Name => Path.GetFileNameWithoutExtension(
        Directory.GetFiles(root, "*.slnx") is [var solution]
            ? solution
            : throw new InitException($"The folder {root} must hold exactly one .slnx file."));

    public string AuthProvider => SettingPattern().Match(File.ReadAllText(AppSettingsPath)) is { Success: true } match
        ? match.Groups["value"].Value
        : throw new InitException($"{AppSettingsPath} has no AuthProvider setting.");

    private string AuthProvidersPath => Path.Combine(root, $"{Name}.AppHost", "AuthProviders");
    private string AppSettingsPath => Path.Combine(root, $"{Name}.AppHost", "appsettings.json");

    public bool RenameIsDue(string newName)
    {
        if (!NamePattern().IsMatch(newName))
            throw new InitException($"'{newName}' is not a C# namespace. Use letters and digits, with dots between the parts.");
        if (Name == newName)
            return false;
        if (Name != TemplateName)
            throw new InitException($"The app has the name {Name} already. The script renames only {TemplateName}.");
        return true;
    }

    // Returns the folder name of the provider, which is the exact value of the setting
    public string KnownProvider(string provider)
    {
        var known = Providers();
        return known.FirstOrDefault(p => string.Equals(p, provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new InitException($"'{provider}' is not a folder in {AuthProvidersPath}. Use one of these: {string.Join(", ", known)}.");
    }

    public void FailIfTreeHasChanges()
    {
        if (Git("status", "--porcelain").Length > 0)
            throw new InitException("The git tree has changes. Commit or stash them, then run the script again.");
    }

    public void Rename(string newName)
    {
        ReplaceInFile(Path.Combine(root, Readme), text => InitSectionPattern().Replace(text, ""));
        var files = GitFiles().Where(file => file != ThisScript).ToList();
        var userSecretsId = Guid.NewGuid().ToString();
        var changed = 0;
        foreach (var file in files)
        {
            if (ReplaceInFile(Path.Combine(root, file), text => ReplaceName(text, newName).Replace(TemplateUserSecretsId, userSecretsId, StringComparison.Ordinal)))
                changed++;
        }

        // Rename the deepest folders first, so that the path of each folder that is still to move stays the same
        var folders = files
            .SelectMany(Ancestors)
            .Distinct()
            .Where(folder => ReplaceName(Path.GetFileName(folder), newName) != Path.GetFileName(folder))
            .OrderByDescending(folder => folder.Count(c => c == '/'));
        foreach (var folder in folders)
        {
            var parent = Path.GetDirectoryName(folder) ?? "";
            Directory.Move(Path.Combine(root, folder), Path.Combine(root, parent, ReplaceName(Path.GetFileName(folder), newName)));
        }

        foreach (var file in files)
        {
            var folder = ReplaceName(Path.GetDirectoryName(file) ?? "", newName);
            var source = Path.Combine(root, folder, Path.GetFileName(file));
            var target = Path.Combine(root, folder, ReplaceName(Path.GetFileName(file), newName));
            if (source != target && File.Exists(source))
                File.Move(source, target);
        }

        File.Delete(Path.Combine(root, InitWorkflow));
        Console.WriteLine($"Renamed {TemplateName} to {newName} in {changed} files.");
    }

    public void SetAuthProvider(string provider)
    {
        ReplaceInFile(AppSettingsPath, text => SettingPattern().Replace(text, $"${{prefix}}{provider}\"", 1));
        Console.WriteLine($"Set the default AuthProvider to {provider}.");
    }

    // A provider is a folder in AuthProviders/ that holds its factory class
    private List<string> Providers() => Directory.GetDirectories(AuthProvidersPath)
        .Where(folder => Directory.GetFiles(folder, "*AuthProvider.cs").Length > 0)
        .Select(Path.GetFileName)
        .OfType<string>()
        .Order(StringComparer.Ordinal)
        .ToList();

    // One pass, so that a new name that holds the template name is not replaced again.
    // Aspire names the project types with underscores, for example Projects.Acme_Shop_Server.
    // The links to the upstream repo, github.com/pdevito3/FullstackTemplate, keep the template name.
    private static string ReplaceName(string text, string newName) => NameFormsPattern().Replace(text, match => match.Value switch
    {
        TemplateName => newName,
        "fullstacktemplate" => newName.ToLowerInvariant(),
        _ => $"Projects.{newName.Replace('.', '_')}_",
    });

    private static IEnumerable<string> Ancestors(string file)
    {
        for (var folder = Path.GetDirectoryName(file); !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder))
            yield return folder.Replace('\\', '/');
    }

    // Writes the file only if the text changes. Keeps a UTF-8 byte order mark, and skips symlinks and binary files.
    private static bool ReplaceInFile(string path, Func<string, string> replace)
    {
        if (!File.Exists(path) || new FileInfo(path).LinkTarget is not null)
            return false;

        var bytes = File.ReadAllBytes(path);
        if (bytes.Contains((byte)0))
            return false;

        var preamble = Encoding.UTF8.Preamble;
        var hasBom = bytes.AsSpan().StartsWith(preamble);
        var text = Encoding.UTF8.GetString(hasBom ? bytes[preamble.Length..] : bytes);
        var replaced = replace(text);
        if (replaced == text)
            return false;

        File.WriteAllBytes(path, [.. hasBom ? preamble : [], .. Encoding.UTF8.GetBytes(replaced)]);
        return true;
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
        using var git = Process.Start(startInfo) ?? throw new InitException("The script could not start git.");
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0)
            throw new InitException($"git {arguments[0]} failed in {root}. Run the script in a git clone of the repo.");

        return output;
    }

    [GeneratedRegex(@"Projects\.FullstackTemplate_|(?<!pdevito3/)FullstackTemplate|(?<!pdevito3/)fullstacktemplate")]
    private static partial Regex NameFormsPattern();

    [GeneratedRegex(@"<!-- init:start .*?<!-- init:end -->\r?\n(\r?\n)?", RegexOptions.Singleline)]
    private static partial Regex InitSectionPattern();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*$")]
    private static partial Regex NamePattern();

    [GeneratedRegex("(?<prefix>\"AuthProvider\"\\s*:\\s*\")(?<value>[^\"]*)\"")]
    private static partial Regex SettingPattern();
}

internal sealed class InitException(string message) : Exception(message);
