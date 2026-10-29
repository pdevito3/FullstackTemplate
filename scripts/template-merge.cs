// Starts a merge of a newer template commit into an app that scripts/init.cs made. See docs/template-updates.md.
// Run it in the folder of the app:
//
//   git fetch template
//   git show template/main:scripts/template-merge.cs > /tmp/template-merge.cs
//   dotnet run /tmp/template-merge.cs
//
// The script renames the template at the merge base and at the new commit with the init.cs of the new commit, so that
// both sides of the three-way merge use the names of the app. It leaves the merge open, as `git merge` does after a
// conflict. Resolve each conflict, then run `git commit`. To stop, run `git merge --abort`.
// init.cs deletes this script in an app, so the script can spell the template name.

using System.Diagnostics;
using System.Text.RegularExpressions;

const string Usage = """
    Usage: dotnet run template-merge.cs -- [--ref <Commit>] [--base <Commit>] [--display-name <DisplayName>]

      --ref <Commit>              The template commit to merge. The default is template/main.
      --base <Commit>             The last template commit that the app has all changes of. The default is the merge
                                  base of the app and --ref. Give it when the app has no template history, or when an
                                  earlier update copied template changes instead of a merge.
      --display-name <DisplayName>
                                  The display name that init.cs gave the app, for example "Acme Shop". The default is
                                  the <title> of frontend/index.html.

    The script needs a clean git tree. It writes the renamed template commits to refs/template-merge/base and
    refs/template-merge/tip, so that `git diff refs/template-merge/base refs/template-merge/tip` shows each change of
    the template in the names of the app.
    """;

var templateRef = "template/main";
string? baseRef = null;
string? displayName = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--ref" when i + 1 < args.Length:
            templateRef = args[++i];
            break;
        case "--base" when i + 1 < args.Length:
            baseRef = args[++i];
            break;
        case "--display-name" when i + 1 < args.Length:
            displayName = args[++i];
            break;
        default:
            Console.Error.WriteLine($"The argument '{args[i]}' is not known or has no value.");
            Console.Error.WriteLine(Usage);
            return 1;
    }
}

try
{
    var app = new App(App.RootOf(Directory.GetCurrentDirectory()));
    app.Merge(templateRef, baseRef, displayName ?? app.DisplayName);
    return 0;
}
catch (MergeException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

internal sealed partial class App(string root)
{
    private const string InitScript = "scripts/init.cs";

    // The git files of a merge, a cherry-pick, a revert, and a rebase that is open
    private static readonly string[] OpenOperations = ["MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply"];

    public static string RootOf(string folder)
        => Git(folder, out var root, "rev-parse", "--show-toplevel") == 0 ? root.Trim() : throw new MergeException("Run the script in the folder of the app. It must be a git repo.");

    public string Name => Path.GetFileNameWithoutExtension(
        Directory.GetFiles(root, "*.slnx") is [var solution]
            ? solution
            : throw new MergeException($"The folder {root} must hold exactly one .slnx file."));

    // init.cs writes the display name to the <title> of the page. A title that init.cs would not accept is not one.
    public string DisplayName
        => File.Exists(Path.Combine(root, "frontend", "index.html"))
            && TitlePattern().Match(File.ReadAllText(Path.Combine(root, "frontend", "index.html"))) is { Success: true } title
            && DisplayNamePattern().IsMatch(title.Groups["title"].Value.Trim())
                ? title.Groups["title"].Value.Trim()
                : Name;

    public void Merge(string templateRef, string? baseRef, string displayName)
    {
        if (Git(root, "status", "--porcelain", "--untracked-files=all").Length > 0)
            throw new MergeException("The git tree has changes. Commit or stash the changes, then run the script again.");
        if (OpenOperations.Any(name => Path.Exists(GitPath(name))))
            throw new MergeException("A merge, a cherry-pick, a revert, or a rebase is open. Finish it or abort it, then run the script again.");

        var tip = Commit(templateRef, $"'{templateRef}' is not a commit. Add the template as a remote and fetch it: git remote add template https://github.com/pdevito3/FullstackTemplate.git && git fetch template");
        if (Git(root, out _, "merge-base", "--is-ancestor", tip, "HEAD") == 0)
            throw new MergeException($"The app has {Short(tip)} already. There is nothing to merge.");
        var baseCommit = baseRef is not null
            ? Commit(baseRef, $"'{baseRef}' is not a commit. Fetch the template, then give a template commit.")
            : Git(root, out var mergeBase, "merge-base", "HEAD", tip) == 0
                ? mergeBase.Trim()
                : throw new MergeException(Git(root, "rev-parse", "--is-shallow-repository").Trim() == "true"
                    ? "The app is a shallow clone, so git cannot find the merge base. Run git fetch --unshallow, then run the script again."
                    : $"The app and {templateRef} have no shared commit. Give the last template commit that the app has all changes of with --base.");
        if (Git(root, out _, "merge-base", "--is-ancestor", baseCommit, tip) != 0)
            throw new MergeException($"{Short(baseCommit)} is not a commit of the template history of {Short(tip)}.");
        if (Git(root, "rev-parse", baseCommit).Trim() == tip)
            throw new MergeException($"The base is {Short(tip)} already. There is nothing to merge.");

        // The first folder or file of each path that has the template name, as .agents/skills/verify-fullstack-template
        var keptPaths = Git(root, "ls-files").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Split('/'))
            .Where(parts => parts.Any(KeepsTemplateName))
            .Select(parts => string.Join('/', parts.TakeWhile(part => !KeepsTemplateName(part)).Append(parts.First(KeepsTemplateName))))
            .Distinct()
            .ToList();
        if (keptPaths.Count > 0)
            throw new MergeException($"These paths of the app keep the template name, and init.cs renames them in the template:\n  {string.Join("\n  ", keptPaths.Take(20))}\nRename them to the name of the app in a commit of their own, as init.cs does, then run the script again. Without that, the merge adds a second copy of each file.");

        Git(root, out var grep, "grep", "-I", "-n", "-E", "FullstackTemplate|fullstacktemplate|fullstack-template|Fullstack Template", "--", ".", $":!{InitScript}");
        var keptLines = grep.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(KeepsTemplateName)
            .ToList();
        if (keptLines.Count > 0)
            Console.WriteLine($"These lines of the app keep the template name. The template side of the merge has the name of the app, so a change of the template to one of these lines gives a conflict:\n  {string.Join("\n  ", keptLines.Take(20))}");

        Console.WriteLine($"Rename the template to {Name}, with the display name \"{displayName}\". If init.cs gave the app another display name, give it with --display-name.");
        var renamedBase = Renamed(baseCommit, tip, displayName);
        var renamedTip = Renamed(tip, tip, displayName);
        Git(root, "update-ref", "refs/template-merge/base", renamedBase);
        Git(root, "update-ref", "refs/template-merge/tip", renamedTip);

        Console.WriteLine($"Merge the template {Short(baseCommit)}..{Short(tip)} ({Git(root, "rev-list", "--count", $"{baseCommit}..{tip}").Trim()} commits) into {Name}.");
        var head = Git(root, "rev-parse", "HEAD").Trim();
        // git names each side of a conflict with the GITHEAD_<commit> variable of its commit
        var labels = new Dictionary<string, string>
        {
            [$"GITHEAD_{head}"] = "HEAD",
            [$"GITHEAD_{renamedTip}"] = $"template {Short(tip)}",
        };
        // merge-recursive exits with 1 for a conflict, and with a higher code when it did not merge
        var merge = Git(root, out _, labels, "merge-recursive", "--renormalize", renamedBase, "--", head, renamedTip);
        if (merge > 1)
            throw new MergeException("git merge-recursive did not merge, and the app has no change. Read the git message above, correct the cause, then run the script again.");

        // A second parent on the real template commit makes it the merge base of the next merge
        File.WriteAllText(GitPath("ORIG_HEAD"), head + "\n");
        File.WriteAllText(GitPath("MERGE_HEAD"), tip + "\n");
        File.WriteAllText(GitPath("MERGE_MODE"), "no-ff");
        File.WriteAllText(GitPath("MERGE_MSG"), $"merge: bring template {Short(tip)} into {Name}\n");

        var conflicts = Git(root, "diff", "--name-only", "--diff-filter=U").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine(merge == 0 && conflicts.Length == 0
            ? "The merge has no conflicts. Build and test, then run git commit."
            : $"The merge has {conflicts.Length} conflicts:\n  {string.Join("\n  ", conflicts)}\nResolve each one (see docs/template-updates.md), build and test, then run git commit.");
        Console.WriteLine("The template commits of the merge: git log --oneline " + $"{Short(baseCommit)}..{Short(tip)}");
        Console.WriteLine("The changes of the template in the names of the app: git diff refs/template-merge/base refs/template-merge/tip");
    }

    // Checks out the commit in a temporary worktree, renames it with the init.cs of the new template commit, and
    // commits the result. The same init.cs on both sides keeps a change of init.cs out of the merge.
    private string Renamed(string commit, string tip, string displayName)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"template-merge-{Guid.NewGuid():N}");
        Git(root, Quiet("worktree", "add", "--quiet", "--detach", folder, commit));
        try
        {
            Git(folder, Quiet("checkout", tip, "--", InitScript));
            Git(folder, Quiet("commit", "--no-verify", "--allow-empty", "--quiet", "-m", "template-merge: the init.cs of the new template commit"));

            Run(folder, "dotnet", "run", InitScript, "--", "--name", Name, "--display-name", displayName);

            // init.cs makes a new user secrets ID, and the app can have another default auth provider. The values of
            // the app keep these lines out of the merge.
            KeepAppValue(folder, $"{Name}.AppHost/{Name}.AppHost.csproj", UserSecretsIdPattern());
            KeepAppValue(folder, $"{Name}.AppHost/appsettings.json", AuthProviderPattern());

            Git(folder, "add", "-A");
            Git(folder, Quiet("commit", "--no-verify", "--allow-empty", "--quiet", "-m", $"template-merge: {Short(commit)} renamed to {Name}"));
            return Git(folder, "rev-parse", "HEAD").Trim();
        }
        finally
        {
            // A failed removal must not hide the error of the step above it. git worktree prune removes the entry later.
            Git(root, out _, "worktree", "remove", "--force", folder);
        }
    }

    // The hooks and the commit signing of the app must not run on the template commits of the script
    private static readonly string NoHooks = Path.Combine(Path.GetTempPath(), $"template-merge-no-hooks-{Guid.NewGuid():N}");

    private static string[] Quiet(params string[] arguments)
        => ["-c", $"core.hooksPath={NoHooks}", "-c", "commit.gpgsign=false", "-c", "user.name=template-merge", "-c", "user.email=template-merge@localhost", .. arguments];

    private void KeepAppValue(string folder, string file, Regex pattern)
    {
        var appFile = Path.Combine(root, file);
        var snapshotFile = Path.Combine(folder, file);
        if (!File.Exists(appFile) || !File.Exists(snapshotFile) || pattern.Match(File.ReadAllText(appFile)) is not { Success: true } appValue)
            return;
        File.WriteAllText(snapshotFile, pattern.Replace(File.ReadAllText(snapshotFile), _ => appValue.Value, 1));
    }

    // A form of the template name that init.cs renames. The name of the app can hold the template name, as
    // Acme.FullstackTemplate does, so the forms of the app name are removed first.
    private bool KeepsTemplateName(string text)
    {
        foreach (var appForm in new[] { $"Projects.{Name.Replace('.', '_')}_", Name, Name.ToLowerInvariant(), Name.ToLowerInvariant().Replace('.', '-') })
            text = text.Replace(appForm, "", StringComparison.Ordinal);
        return TemplateName().IsMatch(text);
    }

    private string Commit(string reference, string error)
        => Git(root, out var commit, "rev-parse", "--verify", "--quiet", $"{reference}^{{commit}}") == 0 ? commit.Trim() : throw new MergeException(error);

    private string Short(string commit) => Git(root, "rev-parse", "--short", commit).Trim();

    private string GitPath(string name) => Path.GetFullPath(Path.Combine(root, Git(root, "rev-parse", "--git-path", name).Trim()));

    private static string Git(string folder, params string[] arguments)
        => Git(folder, out var output, arguments) == 0 ? output : throw new MergeException($"git {string.Join(' ', arguments)} failed in {folder}.");

    private static int Git(string folder, out string output, params string[] arguments)
        => Git(folder, out output, new Dictionary<string, string>(), arguments);

    private static int Git(string folder, out string output, Dictionary<string, string> environment, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
        };
        foreach (var (name, value) in environment)
            startInfo.Environment[name] = value;
        using var git = Process.Start(startInfo) ?? throw new MergeException("The script could not start git.");
        output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        return git.ExitCode;
    }

    private static void Run(string folder, string command, params string[] arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(command, arguments) { WorkingDirectory = folder })
            ?? throw new MergeException($"The script could not start {command}.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new MergeException($"{command} {string.Join(' ', arguments)} failed in {folder}.");
    }

    // The forms of the template name that init.cs renames. A link to the template repo keeps the name.
    [GeneratedRegex(@"Projects\.FullstackTemplate_|(?<!pdevito3/)FullstackTemplate|(?<!pdevito3/)fullstacktemplate|fullstack-template|Fullstack Template")]
    private static partial Regex TemplateName();

    [GeneratedRegex("<title>(?<title>[^<]*)</title>")]
    private static partial Regex TitlePattern();

    // The display names that init.cs accepts
    [GeneratedRegex(@"^[\p{L}\p{M}\p{N}]+([ .'-][\p{L}\p{M}\p{N}]+)*\z")]
    private static partial Regex DisplayNamePattern();

    [GeneratedRegex("<UserSecretsId>[^<]*</UserSecretsId>")]
    private static partial Regex UserSecretsIdPattern();

    [GeneratedRegex("\"AuthProvider\"\\s*:\\s*\"[^\"]*\"")]
    private static partial Regex AuthProviderPattern();
}

internal sealed class MergeException(string message) : Exception(message);
