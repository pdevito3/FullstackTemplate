#!/usr/bin/env bash
# Starts an isolated stack for this checkout, waits until it is healthy, and writes stack.json.
# Usage: launch.sh [RUN_ID]
set -euo pipefail
source "$(dirname "$0")/lib.sh"

if [[ -n "$(ps_entry)" ]]; then
  if [[ -f "$CURRENT_FILE" ]]; then
    echo "launch: a verification stack already runs for this checkout (run $(cat "$CURRENT_FILE"))." >&2
    echo "launch: run doctor.sh to reuse it, or cleanup.sh to stop it." >&2
  else
    echo "launch: a stack that this skill did not start runs for this checkout. Do not drive it or stop it." >&2
    echo "launch: ask the user to stop it, or use a git worktree." >&2
  fi
  exit 1
fi

RUN_ID="${1:-$(date +%Y%m%d-%H%M%S)}"
RUN_DIR="$VERIFY_DIR/$RUN_ID"
mkdir -p "$RUN_DIR"

echo "launch: starting the isolated stack (run $RUN_ID)..."
(cd "$ROOT" && dotnet tool restore >/dev/null)
start_json="$(aspire start --isolated --format Json --apphost "$APPHOST_DIR")"
echo "$start_json" > "$RUN_DIR/aspire-start.json"
echo "$RUN_ID" > "$CURRENT_FILE"

for r in server bff webfrontend; do
  echo "launch: waiting for $r..."
  aspire wait "$r" --status healthy --timeout 300 --apphost "$APPHOST_DIR" >/dev/null
done

web="$(url_of webfrontend http)"
# The SPA can serve HTML before the Vite proxy reaches the BFF. Wait for an API answer through it.
for _ in $(seq 1 60); do
  code="$(curl -s -o /dev/null -w '%{http_code}' -H 'X-CSRF: 1' "$web/api/v1/weather" || true)"
  [[ "$code" == "200" ]] && break
  sleep 2
done
[[ "$code" == "200" ]] || { echo "launch: $web/api/v1/weather answered $code, not 200." >&2; exit 1; }

jq -n \
  --arg runId "$RUN_ID" \
  --arg gitRev "$(git -C "$ROOT" rev-parse HEAD)" \
  --arg dirty "$(git -C "$ROOT" status --porcelain | wc -l | tr -d ' ')" \
  --argjson start "$start_json" \
  --arg web "$web" \
  --arg bff "$(url_of bff https)" \
  --arg server "$(url_of server https)" \
  '{runId: $runId, gitRev: $gitRev, dirtyFiles: ($dirty | tonumber), appHostPath: $start.appHostPath,
    appHostPid: $start.appHostPid, dashboardUrl: $start.dashboardUrl,
    urls: {web: $web, bff: $bff, server: $server}}' > "$RUN_DIR/stack.json"

echo "launch: ready."
echo "RUN_DIR=$RUN_DIR"
jq -r '"web=\(.urls.web)\nserver=\(.urls.server)\ndashboard=\(.dashboardUrl)"' "$RUN_DIR/stack.json"
