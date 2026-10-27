"""Prove that a diff only moves lines.

Usage: python3 -I check-moves.py <repo> <base> <head|WORKTREE> <source> <path>...
Counts each removed line and each added line in the diff of the paths (untracked files count as added in WORKTREE
mode). Also checks that each other path is made of runs of the source in source order, so no line moved inside a
block. Prints each line that is removed and not added again, or added and not removed, with its count. Exit 0 only
when the two multisets are equal.
"""
import collections, difflib, pathlib, subprocess, sys

repo, base, head, source, *targets = sys.argv[1:]
paths = [source, *targets]
def git(*args):
    return subprocess.run(["git", "-C", repo, *args], check=True, capture_output=True).stdout

if head == "WORKTREE":
    subprocess.run(["git", "-C", repo, "add", "-N", "--", *paths], check=True)
    diff = git("diff", "-U0", "--no-color", "--no-renames", base, "--", *paths)
else:
    diff = git("diff", "-U0", "--no-color", "--no-renames", base, head, "--", *paths)

removed, added = collections.Counter(), collections.Counter()
for line in diff.splitlines():
    if line.startswith((b"---", b"+++")):
        continue
    if line.startswith(b"-"):
        removed[line[1:]] += 1
    elif line.startswith(b"+"):
        added[line[1:]] += 1

old = git("show", f"{base}:{source}").splitlines()
out_of_order = 0
for target in targets:
    new = (pathlib.Path(repo) / target).read_bytes() if head == "WORKTREE" else git("show", f"{head}:{target}")
    new = new.splitlines()
    matched = sum(b.size for b in difflib.SequenceMatcher(None, old, new, autojunk=False).get_matching_blocks())
    if matched != len(new):
        print(f"OUT OF SOURCE ORDER: {target} has {len(new) - matched} lines outside runs of {source}")
        out_of_order += 1

only_removed, only_added = removed - added, added - removed
for label, counter in (("REMOVED, NOT ADDED", only_removed), ("ADDED, NOT REMOVED", only_added)):
    for text, n in sorted(counter.items()):
        print(f"{label} x{n}: {text.decode()!r}")
print(f"moved lines: {sum((removed & added).values())}, unmatched: {sum(only_removed.values()) + sum(only_added.values())}")
sys.exit(0 if not only_removed and not only_added and not out_of_order else 1)
