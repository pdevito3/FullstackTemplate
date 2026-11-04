#!/usr/bin/env bash
# Fails while a process runs in this checkout. A leftover can hold ports that the stacks of other worktrees need.
# Usage: leftover-processes.sh [SECONDS]    (waits up to SECONDS for the processes to stop)
# Exit code: 0 no leftover, 1 a leftover runs, 2 the script cannot do the check.
set -uo pipefail
source "$(dirname "$0")/lib.sh"
[[ -n "$ROOT" ]] || { echo "cleanup: run this script in a git checkout." >&2; exit 2; }
# lsof prints these characters as escape codes, so its paths cannot match the path of this checkout.
[[ "$ROOT" != *[\\[:cntrl:]]* ]] || { echo "cleanup: cannot check a checkout whose path has a backslash or a control character." >&2; exit 2; }

# The commands of this script must not have their cwd in the checkout, or they count themselves.
cd /

# Prints <pid> TAB <cwd> for each process. macOS has no /proc, but it has lsof.
process_cwds() {
  if [[ -e /proc/self/cwd ]]; then
    local link cwd pid
    for link in /proc/[0-9]*/cwd; do
      cwd="$(readlink "$link" 2>/dev/null)" || continue
      pid="${link#/proc/}"
      printf '%s\t%s\n' "${pid%/cwd}" "$cwd"
    done
  else
    LC_ALL=en_US.UTF-8 lsof -a -d cwd -Fn 2>/dev/null | awk '/^p/ { pid = substr($0, 2) } /^n/ { print pid "\t" substr($0, 2) }'
  fi
}

in_nested_worktree() {
  local other
  while IFS= read -r other; do
    [[ "$other" == "$ROOT"/* && ( "$1" == "$other" || "$1" == "$other"/* ) ]] && return 0
  done <<<"$worktrees"
  return 1
}

in_this_checkout() {
  [[ "$1" == "$ROOT" || "$1" == "$ROOT"/* ]] && ! in_nested_worktree "$1"
}

# These run for the session of a person or an agent, not for the work: a terminal shell with no script, an agent CLI,
# the MCP servers of .mcp.json, and the caffeinate of the agent CLI. The whole command must match, so that a server with
# one of these names in a path or an argument still counts.
is_session_process() {
  local executable="${1%% *}" arguments="${1#"${1%% *}"}"
  [[ "${executable##*/}$arguments" =~ ^(-?(bash|zsh|sh|fish)( (-i|-l|-il|-li|--login))?|claude( .*)?|(dotnet\ )?aspire\ agent\ mcp|npm\ exec\ @playwright/mcp@latest|sh\ -c\ playwright-mcp|node\ [^\ ]*/\.bin/playwright-mcp|caffeinate\ -i\ -t\ [0-9]+)\ *$ ]]
}

# Prints <pid> TAB <cwd> TAB <command> for each leftover.
list_leftovers() {
  local rows pid cwd command
  rows="$(process_cwds)"
  grep -q "^$$"$'\t' <<<"$rows" && ps -p $$ >/dev/null || return 2
  while IFS=$'\t' read -r pid cwd; do
    in_this_checkout "$cwd" || continue
    [[ "$ancestors" == *" $pid "* ]] && continue
    command="$(ps -o command= -p "$pid")" || continue
    is_session_process "$command" || printf '%s\t%s\t%s\n' "$pid" "$cwd" "$command"
  done <<<"$rows"
}

worktrees="$(git -C "$ROOT" worktree list --porcelain | sed -n 's/^worktree //p')"

ancestors=" "
pid=$$
while [[ -n "$pid" && "$pid" != 0 ]]; do
  ancestors+="$pid "
  pid="$(ps -o ppid= -p "$pid" | tr -d ' ')"
done

deadline=$((SECONDS + ${1:-0}))
while :; do
  leftovers="$(list_leftovers)" || {
    echo "cleanup: cannot read the cwd of the processes, so cannot prove that none is left. This script needs ps, and /proc or lsof." >&2
    exit 2
  }
  [[ -z "$leftovers" ]] && { echo "cleanup: no process is left in this checkout."; exit 0; }
  (( SECONDS >= deadline )) && break
  sleep 1
done

echo "cleanup: these processes still run in this checkout:" >&2
while IFS=$'\t' read -r pid cwd command; do
  echo "  pid $pid in $cwd: $command" >&2
done <<<"$leftovers"
echo "cleanup: stop each process that you started, then run cleanup.sh again. A command in the same pipe as cleanup.sh, such as tail, counts, so run cleanup.sh with no pipe. If you did not start a process, do not stop it. Tell the user." >&2
exit 1
