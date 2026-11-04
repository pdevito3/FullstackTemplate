#!/usr/bin/env bash
# Runs leftover-processes.sh against real processes in temporary worktrees. Runs on macOS and Linux.
# Usage: leftover-processes.test.sh    (prints PASSED or FAILED, and needs no stack)
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

# start <dir> <argv0> [args...]: runs sleep with that name in that folder, and sets $pid.
start() {
  local dir="$1" name="$2"
  shift 2
  (cd "$dir" && exec -a "$name" sleep 300 "$@") &
  pid=$!
  started+=("$pid")
  while [[ "$(ps -o command= -p "$pid")" != "$name"* ]]; do sleep 0.1; done
}

stop() { kill "$pid"; wait "$pid" 2>/dev/null; }

# check <name> <expected exit> <expected text or ""> <dir> [helper args...]. The extra `true` keeps the parent shell
# alive, in the folder, as the parent of the helper.
check() {
  local name="$1" want="$2" text="$3" dir="$4" out code
  shift 4
  out="$(cd "$dir" && bash -c '"$@"; code=$?; true; exit $code' _ "$helper" "$@" 2>&1)"
  code=$?
  if [[ "$code" == "$want" && "$out" == *"$text"* ]]; then
    echo "ok    $name"
  else
    echo "FAIL  $name: exit $code, want $want. Output: $out"
    failed=1
  fi
}

check "a clean checkout passes, its parent shell does not count" 0 "no process is left" "$base/foo"

start "$base/foo" sleep; check "a leftover at the root refuses and names pid and command" 1 "pid $pid in $base/foo: sleep 300" "$base/foo"; stop
start "$base/foo/sub" sleep; check "a leftover in a subfolder refuses" 1 "pid $pid" "$base/foo"; stop
check "the checkout passes after the leftover stops" 0 "" "$base/foo"

start "$base/foo-bar" sleep; check "a worktree whose path starts with this path does not count" 0 "" "$base/foo"
check "the same process refuses in its own worktree" 1 "pid $pid" "$base/foo-bar"; stop

start "$base/foo" sleep; check "a worktree whose path is the prefix of this path does not count" 0 "" "$base/foo-bar"; stop

start "$base/foo/nested" sleep; check "a worktree in a folder of this checkout does not count" 0 "" "$base/foo"
check "the same process refuses in the nested worktree" 1 "pid $pid" "$base/foo/nested"; stop
start "$base/foo" sleep; check "a process of the outer checkout does not count in the nested worktree" 0 "" "$base/foo/nested"; stop

for session in -zsh claude "/opt/homebrew/bin/claude" "/x/aspire agent mcp" "npm exec @playwright/mcp@latest" "node /x/.bin/playwright-mcp" caffeinate; do
  start "$base/foo" "$session"; check "a session process does not count: $session" 0 "" "$base/foo"; stop
done
start "$base/foo" python3; check "a script process counts" 1 "python3" "$base/foo"; stop

(cd "$base/foo" && exec sleep 2) &
check "the wait passes when the process stops in time" 0 "" "$base/foo" 10

# check_failed_scan <tool>: replaces the tool with one that fails. A scan that cannot see the processes must not pass.
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
check_failed_scan ps
[[ -e /proc/self/cwd ]] || check_failed_scan lsof

(( failed == 0 )) && echo "leftover-processes: PASSED" || echo "leftover-processes: FAILED"
exit "$failed"
