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
           dotnet run template-merge.cs -- --find-base [--ref <Commit>] [--display-name <DisplayName>]

      --ref <Commit>              The template commit to merge. The default is template/main.
      --base <Commit>             The last template commit that the app has all changes of. The default is the merge
                                  base of the app and --ref. Give it when the app has no template history, or when an
                                  earlier update copied template changes instead of a merge.
      --display-name <DisplayName>
                                  The display name that init.cs gave the app, for example "Acme Shop". The default is
                                  the <title> of frontend/index.html.
      --find-base                 Print the newest template commit that the app has all changes of, and stop. The
                                  script finds it by the subjects of the commits of the app, and by the heads of the
                                  template pull requests that the app merged. Give it to the merge with --base.

    The script needs a clean git tree. It writes the renamed template commits to refs/template-merge/base and
    refs/template-merge/tip, so that `git diff refs/template-merge/base refs/template-merge/tip` shows each change of
    the template in the names of the app.
    """;

var templateRef = "template/main";
string? baseRef = null;
string? displayName = null;
var findBase = false;
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
        case "--find-base":
            findBase = true;
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
    if (findBase)
        app.PrintBase(templateRef, displayName ?? app.DisplayName);
    else
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
        using var runLock = RunLock();
        if (Git(root, "status", "--porcelain", "--untracked-files=all").Length > 0)
            throw new MergeException("The git tree has changes. Commit or stash the changes, then run the script again.");
        if (OpenOperations.Any(name => Path.Exists(GitPath(name))))
            throw new MergeException("A merge, a cherry-pick, a revert, or a rebase is open. Finish it or abort it, then run the script again.");
        RemoveStaleCopies();

        var tip = Tip(templateRef);
        var baseCommit = baseRef is not null
            ? Commit(baseRef, $"'{baseRef}' is not a commit. Fetch the template, then give a template commit.")
            : MergeBase(tip) is { } mergeBase
                ? mergeBase
                : throw new MergeException($"The app and {templateRef} have no shared commit. Give the last template commit that the app has all changes of with --base. To find it, run the script with --find-base.");
        if (baseRef is null && FoundBase(templateRef, tip, baseCommit, displayName) is ({ } found, var subjectOnly) && found != baseCommit)
            throw new MergeException($"The merge base {Describe(baseCommit)} is old. The app seems to have all changes of the newer template commit {Describe(found)}, because an earlier update merged the branch of a template pull request or copied its commits.{SubjectOnlyNote(subjectOnly)}\nRun the script again with --base {Short(found)}. To merge from the merge base, give --base {Short(baseCommit)}.");
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
        // A second parent on the real template commit makes it the merge base of the next merge. The script writes
        // the merge state before it merges, so that git merge --abort works if the script stops during the merge.
        string[] mergeState = ["MERGE_HEAD", "MERGE_MODE", "MERGE_MSG"];
        File.WriteAllText(GitPath("ORIG_HEAD"), head + "\n");
        File.WriteAllText(GitPath("MERGE_HEAD"), tip + "\n");
        File.WriteAllText(GitPath("MERGE_MODE"), "no-ff");
        File.WriteAllText(GitPath("MERGE_MSG"), $"merge: bring template {Short(tip)} into {Name}\n");

        // merge-recursive exits with 1 for a conflict, and with a higher code when it did not merge
        var merge = Git(root, out _, labels, "merge-recursive", "--renormalize", renamedBase, "--", head, renamedTip);
        if (merge > 1)
        {
            foreach (var name in mergeState)
                File.Delete(GitPath(name));
            throw new MergeException("git merge-recursive did not merge, and the app has no change. Read the git message above, correct the cause, then run the script again.");
        }

        var conflicts = Git(root, "diff", "--name-only", "--diff-filter=U").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine(merge == 0 && conflicts.Length == 0
            ? "The merge has no conflicts. Build and test, then run git commit."
            : $"The merge has {conflicts.Length} conflicts:\n  {string.Join("\n  ", conflicts)}\nResolve each one (see docs/template-updates.md), build and test, then run git commit.");
        Console.WriteLine("The template commits of the merge: git log --oneline " + $"{Short(baseCommit)}..{Short(tip)}");
        Console.WriteLine("The changes of the template in the names of the app: git diff refs/template-merge/base refs/template-merge/tip");
    }

    public void PrintBase(string templateRef, string displayName)
    {
        var tip = Tip(templateRef);
        var mergeBase = MergeBase(tip);
        Console.WriteLine(mergeBase is null ? $"The app and {templateRef} have no shared commit." : $"The merge base is {Describe(mergeBase)}.");
        Console.WriteLine(FoundBase(templateRef, tip, mergeBase, displayName) is ({ } found, var subjectOnly)
            ? $"The newest template commit that the app has all changes of is {Describe(found)}.{SubjectOnlyNote(subjectOnly)}\nMerge with: dotnet run template-merge.cs -- --base {Short(found)}"
            : "The app has no template commit. Give the template commit that init.cs made the app from with --base.");
    }

    private string Tip(string templateRef)
    {
        var tip = Commit(templateRef, $"'{templateRef}' is not a commit. Add the template as a remote and fetch it: git remote add template https://github.com/pdevito3/FullstackTemplate.git && git fetch template");
        return Git(root, out _, "merge-base", "--is-ancestor", tip, "HEAD") == 0
            ? throw new MergeException($"The app has {Short(tip)} already. There is nothing to merge.")
            : tip;
    }

    private string? MergeBase(string tip)
        => Git(root, out var mergeBase, "merge-base", "HEAD", tip) == 0
            ? mergeBase.Trim()
            : Git(root, "rev-parse", "--is-shallow-repository").Trim() == "true"
                ? throw new MergeException("The app is a shallow clone, so git cannot find the merge base. Run git fetch --unshallow, then run the script again.")
                : null;

    // Walks the template history from the merge base to the tip, oldest first, and stops at the first commit that the
    // app does not have. The app has a commit when the commit is in its history, when a commit of the app after the
    // merge base has the same subject (a copy, or the squash of a pull request whose branch the app merged), or when
    // the app merged the head of its pull request. A walk that stops early gives more conflicts and loses no template
    // change. A subject that the app has for another reason, also one with the same pull request number, can take the
    // walk too far, so the walk also gives each commit that matched only by its subject and not by its changes, for the
    // user to check.
    private (string? Commit, List<string> SubjectOnly) FoundBase(string templateRef, string tip, string? mergeBase, string displayName)
    {
        var appCommits = Lines(Git(root, "rev-list", "HEAD")).ToHashSet(StringComparer.Ordinal);
        var appCommitsBySubject = Lines(mergeBase is null ? Git(root, "log", "--format=%H%x00%s", "HEAD") : Git(root, "log", "--format=%H%x00%s", "HEAD", "--not", mergeBase))
            .Select(line => line.Split('\0'))
            .ToLookup(parts => parts[1], parts => parts[0], StringComparer.Ordinal);
        var pullHeads = PullHeads(templateRef);
        string[] appForms = [.. new[] { $"Projects.{Name.Replace('.', '_')}_", Name, Name.ToLowerInvariant(), Name.ToLowerInvariant().Replace('.', '-'), displayName }
            .OrderByDescending(form => form.Length)];
        var subjectOnly = new List<string>();
        bool Has(string commit, string subject)
        {
            if (appCommits.Contains(commit))
                return true;
            var pull = PullNumber().Match(subject);
            if (pull.Success && pullHeads.TryGetValue(pull.Groups["number"].Value, out var head) && appCommits.Contains(head))
                return true;
            if (!appCommitsBySubject.Contains(subject))
                return false;
            var changes = ChangesOf(commit, line => TemplateName().Replace(line, NamePlaceholder));
            if (!appCommitsBySubject[subject].Any(copy => ChangesOf(copy, line => appForms.Aggregate(line, (text, form) => text.Replace(form, NamePlaceholder, StringComparison.Ordinal))) == changes))
                subjectOnly.Add(commit);
            return true;
        }

        var found = mergeBase;
        foreach (var line in Lines(Git(root, "log", "--first-parent", "--reverse", "--format=%H %P%x00%s", mergeBase is null ? tip : $"{mergeBase}..{tip}")))
        {
            var (hashes, subject) = (line.Split('\0')[0].Split(' '), line.Split('\0')[1]);
            // A merge commit of the template brings the commits of its branch. The app has it when it has each of them.
            var branch = hashes.Length > 2 ? Lines(Git(root, "log", "--format=%H%x00%s", $"{hashes[1]}..{hashes[2]}")) : [];
            var has = appCommits.Contains(hashes[0])
                || (branch.Length > 0 ? branch.All(branchLine => Has(branchLine.Split('\0')[0], branchLine.Split('\0')[1])) : Has(hashes[0], subject));
            if (!has)
                break;
            found = hashes[0];
        }

        return (found, subjectOnly.Where(commit => found is not null && Git(root, out _, "merge-base", "--is-ancestor", commit, found) == 0).ToList());
    }

    // The commits that only a subject matched, as lines for a message
    private string SubjectOnlyNote(List<string> commits)
        => commits.Count == 0
            ? ""
            : $"\nAn app commit has the subject of each of these template commits, but not the same changes. Check that the app has their changes:\n  {string.Join("\n  ", commits.Select(Describe))}";

    private const string NamePlaceholder = "<name>";

    // The diff of the commit with each form of the name made one placeholder, and with no blob IDs or line numbers, so
    // that a copy of a template commit in the app has the same changes as the template commit
    private string ChangesOf(string commit, Func<string, string> withoutName)
        => string.Join('\n', Lines(Git(root, "show", "--format=", "--no-color", "--no-ext-diff", "--no-show-signature", commit))
            .Where(line => !DiffLineWithNumbers().IsMatch(line))
            .Select(withoutName));

    // The head commit of each pull request of the template remote, by number. An empty map when the remote has none or
    // cannot be reached: then the subjects alone find the base.
    private Dictionary<string, string> PullHeads(string templateRef)
    {
        var remotes = Lines(Git(root, "remote"));
        var remote = remotes.Contains(templateRef.Split('/')[0]) ? templateRef.Split('/')[0] : "template";
        if (!remotes.Contains(remote) || Git(root, out var heads, "ls-remote", remote, "refs/pull/*/head") != 0)
            return [];
        return Lines(heads)
            .Select(line => line.Split('\t'))
            .ToDictionary(parts => parts[1].Split('/')[2], parts => parts[0]);
    }

    private static string[] Lines(string output) => output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private string Describe(string commit) => Git(root, "log", "-1", "--format=%h %s", commit).Trim();

    // Two runs in one repo share the temporary worktrees and the refs/template-merge refs, so a run holds this lock to
    // its end. The system releases the lock of a killed run.
    private FileStream RunLock()
    {
        var path = Path.GetFullPath(Path.Combine(root, Git(root, "rev-parse", "--git-common-dir").Trim(), "template-merge.lock"));
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            throw new MergeException("Another run of the script merges into this repo now. Wait until it ends, then run the script again.");
        }
    }

    // A run that was killed during the rename keeps its temporary worktree registered, and its folder can stay
    private void RemoveStaleCopies()
    {
        var stale = Lines(Git(root, "worktree", "list", "--porcelain"))
            .Where(line => line.StartsWith("worktree ", StringComparison.Ordinal))
            .Select(line => line["worktree ".Length..])
            .Where(path => CopyFolderName().IsMatch(Path.GetFileName(path)))
            .ToList();
        foreach (var path in stale.Where(Directory.Exists))
            Git(root, out _, "worktree", "remove", "--force", path);
        if (stale.Any(path => !Directory.Exists(path)))
            Git(root, out _, "worktree", "prune");
    }

    // Checks out the commit in a temporary worktree, renames it with the init.cs of the new template commit, and
    // commits the result. The same init.cs on both sides keeps a change of init.cs out of the merge.
    private string Renamed(string commit, string tip, string displayName)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"{CopyFolderPrefix}{Guid.NewGuid():N}");
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

    // The output of init.cs is about the renamed copy of the template, not the app, so it shows only on a failure.
    private static void Run(string folder, string command, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(command, arguments)
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(startInfo) ?? throw new MergeException($"The script could not start {command}.");
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new MergeException($"{output}{error.Result}{command} {string.Join(' ', arguments)} failed in {folder}.");
    }

    // The forms of the template name that init.cs renames. A link to the template repo keeps the name.
    [GeneratedRegex(@"Projects\.FullstackTemplate_|(?<!pdevito3/)FullstackTemplate|(?<!pdevito3/)fullstacktemplate|fullstack-template|Fullstack Template")]
    private static partial Regex TemplateName();

    // The folder of a temporary worktree of Renamed
    private const string CopyFolderPrefix = "template-merge-";

    [GeneratedRegex("^" + CopyFolderPrefix + "[0-9a-f]{32}$")]
    private static partial Regex CopyFolderName();

    // The lines of a diff that hold blob IDs, line numbers, or a rename percentage
    [GeneratedRegex("^(index |@@|similarity index |dissimilarity index )")]
    private static partial Regex DiffLineWithNumbers();

    // The number that a squash merge of GitHub puts at the end of the subject, as "fix: a fault (#94)"
    [GeneratedRegex(@"\(#(?<number>\d+)\)$")]
    private static partial Regex PullNumber();

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
