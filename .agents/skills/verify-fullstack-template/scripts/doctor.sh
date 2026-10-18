#!/usr/bin/env bash
# Read-only. Answers "is the verification stack of this checkout worth driving?"
# Exit code 0 means yes. Each line starts with ok or FAIL.
set -uo pipefail
source "$(dirname "$0")/lib.sh"

fails=0
ok()   { echo "ok    $*"; }
fail() { echo "FAIL  $*"; fails=$((fails + 1)); }

run_dir="$(current_run_dir)" || { fail "no current run ($CURRENT_FILE is missing). Run launch.sh."; exit 1; }
stack="$run_dir/stack.json"
[[ -f "$stack" ]] || { fail "$stack is missing. launch.sh did not finish. Run cleanup.sh, then launch.sh."; exit 1; }
ok "run $(jq -r .runId "$stack") at $run_dir"

entry="$(ps_entry)"
want_pid="$(jq -r .appHostPid "$stack")"
if [[ -z "$entry" ]]; then
  fail "no AppHost runs for $APPHOST_CSPROJ"
elif [[ "$(echo "$entry" | jq -r .appHostPid)" != "$want_pid" ]]; then
  fail "the AppHost pid is $(echo "$entry" | jq -r .appHostPid), not $want_pid. This stack is not ours. Do not drive it."
else
  ok "AppHost pid $want_pid is ours"
fi

while read -r name state health urls; do
  case "$name" in
    webfrontend-installer) [[ "$state" == "Finished" ]] && ok "$name Finished" || fail "$name is $state" ;;
    *) [[ "$state" == "Running" && "$health" == "Healthy" ]] && ok "$name $state $health" || fail "$name is $state $health" ;;
  esac
done < <(resource_table)

web="$(jq -r .urls.web "$stack")"
server="$(jq -r .urls.server "$stack")"
[[ "$(url_of webfrontend http)" == "$web" ]] && ok "web URL $web" || fail "the web URL changed. stack.json is stale."

code="$(curl -s -o /dev/null -w '%{http_code}' -H 'X-CSRF: 1' "$web/api/v1/weather")"
[[ "$code" == "200" ]] && ok "GET $web/api/v1/weather -> 200" || fail "GET $web/api/v1/weather -> $code"
code="$(curl -s -o /dev/null -w '%{http_code}' -H 'X-CSRF: 1' "$web/bff/user")"
[[ "$code" == "401" || "$code" == "200" ]] && ok "GET /bff/user -> $code" || fail "GET /bff/user -> $code"
code="$(curl -sk -o /dev/null -w '%{http_code}' "$server/health")"
[[ "$code" == "200" ]] && ok "GET $server/health -> 200" || fail "GET $server/health -> $code"

# The local auth provider is the public Duende demo server. Login fails without it.
code="$(curl -s -o /dev/null -m 10 -w '%{http_code}' https://demo.duendesoftware.com/.well-known/openid-configuration)"
[[ "$code" == "200" ]] && ok "Duende demo IdP reachable" || fail "Duende demo IdP -> $code. Login recipes cannot pass."

rev="$(git -C "$ROOT" rev-parse HEAD)"
[[ "$rev" == "$(jq -r .gitRev "$stack")" ]] && ok "HEAD $rev matches the launch" \
  || echo "warn  HEAD moved since launch. Frontend code hot-reloads. For server or BFF changes, run: dotnet aspire resource server rebuild --apphost $APPHOST_DIR"

exit $(( fails > 0 ))
