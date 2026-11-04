#!/usr/bin/env bash
# Runs leftover-processes.sh against real processes in temporary worktrees, on macOS and Linux. Needs no stack.
set -uo pipefail

helper="$(cd "$(dirname "$0")" && pwd)/leftover-processes.sh"
base="$(cd "$(mktemp -d)" && pwd -P)"
started=()
failed=0
trap 'for p in "${started[@]:-}"; do [[ -n "$p" ]] && kill "$p" 2>/dev/null; done; rm -rf "$base"' EXIT

git init -q "$base/foo"
git -C "$base/foo" -c user.name=t -c user.email=t@t commit -q --allow-empty -m init
git -C "$base/foo" worktree add -q "$base/foo-bar"
git -C "$base/foo" worktree add -q "$base/foo/nested"
mkdir "$base/foo/sub"
mkfifo "$base/pipe"

# start <dir> <command line>: runs a process that ps shows with exactly that command line, and sets $pid. The process is
# a cat that waits on a pipe which it also holds open, so it adds no argument of its own and does not stop.
start() {
  local dir="$1" name="$2"
  (cd "$dir" && exec 3<>"$base/pipe" && exec -a "$name" cat <&3) &
  pid=$!
  started+=("$pid")
  while [[ "$(ps -o command= -p "$pid")" != "$name"* ]]; do sleep 0.1; done
}

stop() { kill "$pid"; wait "$pid" 2>/dev/null; }

# check <name> <expected exit> <expected text or ""> <dir> [helper args...]: runs the helper under a parent shell in <dir>.
check() {
  local name="$1" want="$2" text="$3" dir="$4" out code
  shift 4
  out="$(cd "$dir" && bash -c '"$@"; code=$?; exit $code' _ "$helper" "$@" 2>&1)"
  code=$?
  if [[ "$code" == "$want" && "$out" == *"$text"* ]]; then
    echo "ok    $name"
  else
    echo "FAIL  $name: exit $code, want $want. Output: $out"
    failed=1
  fi
}

check "a clean checkout passes, its parent shell does not count" 0 "no process is left" "$base/foo"

start "$base/foo" sleep; check "a leftover at the root refuses and names pid and command" 1 "pid $pid in $base/foo: sleep" "$base/foo"; stop
start "$base/foo/sub" sleep; check "a leftover in a subfolder refuses" 1 "pid $pid" "$base/foo"; stop
check "the checkout passes after the leftover stops" 0 "" "$base/foo"

start "$base/foo-bar" sleep; check "a worktree whose path starts with this path does not count" 0 "" "$base/foo"
check "the same process refuses in its own worktree" 1 "pid $pid" "$base/foo-bar"; stop

start "$base/foo" sleep; check "a worktree whose path is the prefix of this path does not count" 0 "" "$base/foo-bar"; stop

start "$base/foo/nested" sleep; check "a worktree in a folder of this checkout does not count" 0 "" "$base/foo"
check "the same process refuses in the nested worktree" 1 "pid $pid" "$base/foo/nested"; stop
start "$base/foo" sleep; check "a process of the outer checkout does not count in the nested worktree" 0 "" "$base/foo/nested"; stop

while IFS= read -r session; do
  start "$base/foo" "$session"; check "a session process does not count: $session" 0 "" "$base/foo"; stop
done <<'COMMANDS'
-zsh
-bash
zsh
/bin/zsh -il
/bin/bash --login
claude
claude --resume abc
/opt/homebrew/bin/claude --resume abc
/x/dotnet aspire agent mcp
/x/aspire agent mcp
npm exec @playwright/mcp@latest
node /x/.npm/_npx/1/node_modules/.bin/playwright-mcp
caffeinate -i -t 300
COMMANDS

while IFS= read -r leftover; do
  start "$base/foo" "$leftover"; check "a process with a session name in its arguments counts: $leftover" 1 "pid $pid" "$base/foo"; stop
done <<'COMMANDS'
python3 squatter.py
python3 -m http.server 0 --directory /x/claude
python3 /x/claude /x/playwright-mcp
/x/playwright-mcp-upgrade/frontend/node_modules/.bin/vite
node /x/playwright-mcp-upgrade/frontend/node_modules/.bin/vite
npm exec @playwright/mcp@latest --port 8931
node /x/.bin/playwright-mcp --port 8931
/x/bash script.sh
zsh -c sleep
caffeinate -i python3 squatter.py
aspire run
COMMANDS

start "$base/foo" sleep; check "the wait ends with a refusal when the process does not stop" 1 "pid $pid" "$base/foo" 1; stop

(cd "$base/foo" && exec sleep 2) &
check "the wait passes when the process stops in time" 0 "" "$base/foo" 10

check_failed_scan() {
  mkdir -p "$base/bin-$1"
  printf '#!/bin/sh\nexit 1\n' > "$base/bin-$1/$1"
  chmod +x "$base/bin-$1/$1"
  local out code
  out="$(cd "$base/foo" && PATH="$base/bin-$1:$PATH" "$helper" 2>&1)"
  code=$?
  if [[ "$code" == 2 ]]; then
    echo "ok    a failed $1 refuses"
  else
    echo "FAIL  a failed $1: exit $code, want 2. Output: $out"
    failed=1
  fi
}
out="$(cd "$base" && "$helper" 2>&1)"
code=$?
if [[ "$code" == 2 ]]; then
  echo "ok    a folder that is not a git checkout refuses"
else
  echo "FAIL  a folder that is not a git checkout: exit $code, want 2. Output: ${out:0:200}"
  failed=1
fi

check_failed_scan ps
[[ -e /proc/self/cwd ]] || check_failed_scan lsof

(( failed == 0 )) && echo "leftover-processes: PASSED" || echo "leftover-processes: FAILED"
exit "$failed"
