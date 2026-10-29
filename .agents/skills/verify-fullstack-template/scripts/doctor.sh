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

while IFS=$'\t' read -r name state health urls; do
  case "$name" in
    webfrontend-installer) [[ "$state" == "Finished" ]] && ok "$name Finished" || fail "$name is $state" ;;
    # Labcoat puts the result of the last test run in its state, for example "○ 523 tests · not run".
    # Aspire then gives no health status, so ask the Labcoat health endpoint.
    tests) [[ "$(curl -s -o /dev/null -w '%{http_code}' "${urls%%,*}/api/health")" == "200" ]] \
      && ok "$name healthy ($state)" || fail "$name is $state, and ${urls%%,*}/api/health is not 200" ;;
    *) [[ "$state" == "Running" && "$health" == "Healthy" ]] && ok "$name $state $health" || fail "$name is $state $health" ;;
  esac
done < <(resource_table)

web="$(jq -r .urls.web "$stack")"
server="$(jq -r .urls.server "$stack")"
build="$(jq -r '.build // "dev"' "$stack")"
[[ "$(web_url "$build")" == "$web" ]] && ok "web URL $web ($build build)" || fail "the web URL changed. stack.json is stale."

code="$(curl -sk -o /dev/null -w '%{http_code}' -H 'X-CSRF: 1' "$web/api/v1/weather")"
[[ "$code" == "200" ]] && ok "GET $web/api/v1/weather -> 200" || fail "GET $web/api/v1/weather -> $code"
code="$(curl -sk -o /dev/null -w '%{http_code}' -H 'X-CSRF: 1' "$web/bff/user")"
[[ "$code" == "204" || "$code" == "200" ]] && ok "GET /bff/user -> $code" || fail "GET /bff/user -> $code"
code="$(curl -sk -o /dev/null -w '%{http_code}' "$server/health")"
[[ "$code" == "200" ]] && ok "GET $server/health -> 200" || fail "GET $server/health -> $code"

# A local provider is a resource of the stack, and the loop above examines it. The Duende demo is a public server.
auth_provider="$(jq -r .authProvider "$stack")"
ok "AuthProvider $auth_provider"
if [[ "$auth_provider" == "DuendeDemo" ]]; then
  code="$(curl -s -o /dev/null -m 10 -w '%{http_code}' https://demo.duendesoftware.com/.well-known/openid-configuration)"
  [[ "$code" == "200" ]] && ok "Duende demo IdP reachable" || fail "Duende demo IdP -> $code. Login recipes cannot pass."
fi

lock_pid="$(cat "$run_dir/recipe.lock" 2>/dev/null || true)"
if [[ -n "$lock_pid" ]] && kill -0 "$lock_pid" 2>/dev/null; then
  echo "warn  the recipe run with pid $lock_pid drives the stack now. browse.mjs refuses a second run until it ends."
elif [[ -f "$run_dir/db-snapshot.json" ]]; then
  echo "warn  a recipe run stopped before it restored the database. The next browse.mjs run restores it first. If that restore fails, delete $run_dir/db-snapshot.json."
fi

rev="$(git -C "$ROOT" rev-parse HEAD)"
[[ "$rev" == "$(jq -r .gitRev "$stack")" ]] && ok "HEAD $rev matches the launch" \
  || if [[ "$build" == "production" ]]; then
    echo "warn  HEAD moved since launch. The production build does not reload. Run cleanup.sh, then launch.sh --preview."
  else
    echo "warn  HEAD moved since launch. Frontend code hot-reloads. For server or BFF changes, run: dotnet aspire resource server rebuild --apphost $APPHOST_DIR"
  fi

exit $(( fails > 0 ))
