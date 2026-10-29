# Take updates from the template

This guide tells how to bring new commits of the template ([pdevito3/FullstackTemplate](https://github.com/pdevito3/FullstackTemplate)) into an app that `scripts/init.cs` made.

A plain `git merge` of the template does not work well. The template has its own name in file names and file contents, and the app has the app name. Thus Git sees a conflict at each change of the template near a name, adds the files again that `init.cs` deleted, and adds new lines that have the template name. The script `scripts/template-merge.cs` of the template prevents this. It renames the template on both sides of the merge with `init.cs` first, and then merges.

## Prepare the app once

Do these steps before the first update. Commit each step alone, so that the merge commit holds only the changes of the template.

1. Add the template as a remote that you cannot push to:

   ```bash
   git remote add template https://github.com/pdevito3/FullstackTemplate.git
   git remote set-url --push template no-push
   git fetch template
   ```

2. Find the newest template commit that the app has all changes of. Get the script as [Run the merge](#run-the-merge) step 2 shows, and run it with `--find-base`:

   ```bash
   dotnet run /tmp/template-merge.cs -- --find-base
   ```

   The script prints the merge base and the commit that it found. The merge base is old when an earlier update merged the branch of a template pull request before GitHub squashed it, or copied or cherry-picked template commits. Then the merge base is the template commit before those changes, and a merge from it gives a conflict at each change that the app has already. The script walks the template history from the merge base to `template/main`. It stops at the first commit that the app does not have. The app has a commit when a commit of the app has the same subject, as `fix(bff): remove wall-clock limits from tests that fail under CPU load (#94)`, or when the app merged the head of its pull request. For example, the merge base of CarePoint was #72, and the script found #94 (`10bc415c`).

   If the found commit is not the merge base, check that the app has the changes of the found commit and of each template commit before it, then give it to the script with `--base`. A short, common subject, such as `chore: update packages`, can match a commit of the app that is not a copy. A subject with a pull request number, such as `feat: d (#4)`, can also match a pull request of the app with the same number. Without `--base`, the script stops and tells you the commit to give. The script lists each template commit that an app commit matched by its subject but not by its changes, so that you can check it. To merge from the merge base anyway, give the merge base with `--base`. If the app has no template history, for example because it came from the GitHub "Use this template" button, give the template commit that the app was made from with `--base`. After one merge with the script, the merge base is correct again.

3. Rename each path of the app that still has the template name. An older `init.cs` did not rename all paths, for example the folder of the verify skill. The script stops and lists these paths. Rename them with `git mv` as `init.cs` does, and change the references to them in the other files.

4. Store the text files with LF line endings, as the template does. If the `.gitattributes` file of the app has no `* text=auto eol=lf` line, copy the file of the template and change the template name in it to the app name:

   ```bash
   git show template/main:.gitattributes > .gitattributes
   # Change the template name in .gitattributes to the app name, then:
   git add --renormalize .
   git add .gitattributes
   git commit -m "chore: store each text file with LF, as the template does"
   ```

   Without this step, a file that has CRLF in the app and LF in the template gives more conflicts. In a dry run of Fleet `feat/v1` with the template at `dbaa6844`, this step took the conflicts from 135 to 101, and the conflicted files with CRLF from 41 to 0.

## Run the merge

1. Start from a clean tree on a new branch:

   ```bash
   git switch -c chore/template-update
   git fetch template
   ```

2. Get the newest script from the template, and run it in the root of the app. Put the script outside the app, because the script needs a clean tree:

   ```bash
   git show template/main:scripts/template-merge.cs > /tmp/template-merge.cs
   dotnet run /tmp/template-merge.cs
   ```

   In PowerShell, use `git show template/main:scripts/template-merge.cs | Set-Content -Encoding utf8 $env:TEMP/template-merge.cs` and `dotnet run $env:TEMP/template-merge.cs`.

   | Option | Description |
   |--------|-------------|
   | `--ref <Commit>` | The template commit to merge. The default is `template/main`. |
   | `--base <Commit>` | The last template commit that the app has all changes of. The default is the merge base. |
   | `--display-name <DisplayName>` | The display name that `init.cs` gave the app. The default is the `<title>` of `frontend/index.html`. |
   | `--find-base` | Print the newest template commit that the app has all changes of, and stop. See [Prepare the app once](#prepare-the-app-once), step 2. |

   The script does these steps:

   - It stops if the tree has changes, if a merge or a rebase is open, if a path of the app still has the template name, or if the merge base is older than the template changes that the app has.
   - It removes each temporary worktree that an earlier run of the script left, for example after a kill.
   - It makes a renamed copy of the base commit and of the new commit. It runs the `init.cs` of the new commit on both, so that a change of `init.cs` does not show as a change of the template.
   - It keeps the `UserSecretsId` of the AppHost and the default `AuthProvider` of the app in both copies.
   - It merges the renamed copies into the app, and leaves the merge open as `git merge` does. The second parent of the merge is the real template commit, so the next update starts from it.

3. Read what changed in the template before you resolve a conflict:

   ```bash
   git log --oneline <base>..<new commit>
   git diff refs/template-merge/base refs/template-merge/tip -- <path>
   ```

   The second command shows each change of the template with the names of the app.

To stop the merge, run `git merge --abort`. The script writes the merge state before it merges, so this also works if the script stops during the merge. If the script was killed during the merge, `git merge --abort` can fail with `index.lock: File exists`. Make sure that no other git command runs in the app, delete `.git/index.lock`, and run `git merge --abort` again. If `git merge --abort` says that there is no merge to abort, for example after an older version of the script stopped during the merge, run `git reset --hard`. Then run `git status`. If it lists untracked files that the merge added, read the list and delete them with `git clean -fd`. The script started from a clean tree, so this removes only the changes of the merge.

## Resolve the conflicts

Each side of a conflict has a label. `HEAD` is the app, and `template <commit>` is the template.

| Conflict | What to do |
|----------|------------|
| The app deleted a file, and the template changed it (`git status` shows `deleted by us`). The app can have deleted a demo of the template, or it can have moved the code to another file | Find the app commit that deleted it with `git log --oneline -1 --diff-filter=DR HEAD -- <path>`, and read it with `git show -M --stat <commit>`. If the app moved the code, read the change of the template with `git diff refs/template-merge/base refs/template-merge/tip -- <path>`, and make the same change where the code is now. Then delete the file again with `git rm <path>`. |
| The template deleted or renamed a file that the app changed (`git status` shows `deleted by them`), such as `ConstraintFieldsTests.cs`, which the template renamed to `ConstraintErrorsTests.cs` | Find the template commit that deleted it with `git log --oneline -1 --diff-filter=DR <base>..<new commit> -- '*/<file name>'`, and read it with `git show -M --stat <commit>`. If the template renamed the file or moved its code, move the changes of the app there, then delete the old file with `git rm <path>`. If the template removed the code and the app still needs its change, keep the file with `git add <path>` and make it build against the new code. |
| The app made its own copy of a template fix, and the template has its fix now (`both added`, or both changed the same lines) | Take the template version with `git checkout --theirs -- <path>`. Then delete the copy of the app if it has another path, and change the callers of the app copy. |
| A package version in `Directory.Packages.props` | Take the higher version. |
| The generated API contract: `<App>.Server/openapi/` and `frontend/src/api/generated*/` | Take one side, then make the files again with the `api-contract` skill. Do not edit these files by hand. |
| The EF model snapshot in `Databases/Migrations/` | Take the snapshot of the app with `git checkout --ours -- <path>`, and keep the migrations of both sides. Then add a migration (see [backend.md](backend.md#migrations)). If the new migration only does what the migrations of the template do, delete its two migration files and keep the snapshot. `ModelSnapshotTests` shows if the snapshot is correct. |
| A line of the app that has the template name | Change the name to the app name. The script lists these lines before the merge. |
| The app and the template changed the same code for different reasons | Read the template commit with `git log -p <base>..<new commit> -- <path>`. Keep the behavior of the app and add the change of the template. |

## Check and commit the merge

1. Run the checks of [AGENTS.md](../AGENTS.md#build-and-test): the build with no warnings, all .NET tests, and the frontend install, lint, tests, e2e tests, and build.
2. For each feature that the template commits changed, use the verify skill of the app to test the feature in the running app.
3. Commit with `git commit`. The script prepared the message. Then delete the two refs of the renamed copies:

   ```bash
   git update-ref -d refs/template-merge/base
   git update-ref -d refs/template-merge/tip
   ```
4. Merge the pull request with a merge commit. A squash merge or a rebase merge removes the template commit from the history of the app, and then the next update starts from the old base again.

## Keep the next update small

- When you find a fault in the template, report it as an issue of the template, and get the fix with the next update. A fix that you copy by hand gives a conflict when the fix of the template comes.
- Do not change a template file only for style.
- Update often. A few commits are easier to merge than many.
