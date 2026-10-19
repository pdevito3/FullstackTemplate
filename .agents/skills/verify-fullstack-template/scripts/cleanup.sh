#!/usr/bin/env bash
# Stops the stack that launch.sh started for this checkout. Keeps all evidence in artifacts/verify/<RUN_ID>/.
set -uo pipefail
source "$(dirname "$0")/lib.sh"

run_dir="$(current_run_dir)" || { echo "cleanup: no current run. Nothing to stop."; exit 0; }
want_pid="$(jq -r '.appHostPid // empty' "$run_dir/stack.json" 2>/dev/null || true)"
[[ -n "$want_pid" ]] || want_pid="$(jq -r '.appHostPid // empty' "$run_dir/aspire-start.json" 2>/dev/null || true)"
entry="$(ps_entry)"

if [[ -z "$entry" ]]; then
  echo "cleanup: the stack is not running."
elif [[ -n "$want_pid" && "$(echo "$entry" | jq -r .appHostPid)" != "$want_pid" ]]; then
  echo "cleanup: the running AppHost (pid $(echo "$entry" | jq -r .appHostPid)) is not the one that run $(basename "$run_dir") started. It stays." >&2
  rm -f "$CURRENT_FILE"
  exit 1
else
  echo "cleanup: stopping AppHost pid $(echo "$entry" | jq -r .appHostPid)..."
  aspire stop --apphost "$APPHOST_DIR" >/dev/null
fi

rm -f "$CURRENT_FILE"
[[ -z "$(ps_entry)" ]] && echo "cleanup: stopped." || { echo "cleanup: the AppHost still runs." >&2; exit 1; }
echo "cleanup: evidence kept at $run_dir"
