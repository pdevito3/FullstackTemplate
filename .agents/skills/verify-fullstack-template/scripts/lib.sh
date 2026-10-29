#!/usr/bin/env bash
# Shared values for the verify-fullstack-template scripts. Source this file. Do not run it.

ROOT="$(git rev-parse --show-toplevel)"
APPHOST_DIR="FullstackTemplate.AppHost"
APPHOST_CSPROJ="$ROOT/$APPHOST_DIR/FullstackTemplate.AppHost.csproj"
VERIFY_DIR="$ROOT/artifacts/verify"
# Holds the RUN_ID of the stack that launch.sh started. cleanup.sh deletes it. The run directory stays.
CURRENT_FILE="$VERIFY_DIR/.current"

aspire() { (cd "$ROOT" && dotnet aspire "$@" --nologo --non-interactive 2>/dev/null); }

# Prints the `aspire ps` entry for the AppHost of this checkout, or nothing.
ps_entry() {
  aspire ps --format Json | jq -c --arg p "$APPHOST_CSPROJ" '.[] | select(.appHostPath == $p)'
}

current_run_dir() {
  [[ -f "$CURRENT_FILE" ]] || return 1
  echo "$VERIFY_DIR/$(cat "$CURRENT_FILE")"
}

# Prints one tab-separated line for each resource:
# <displayName> TAB <state> TAB <health> TAB <url,url>.
resource_table() {
  aspire describe --apphost "$APPHOST_DIR" --format Json \
    | jq -r '.resources[] | [.displayName, .state, (.healthStatus // "-"), ([.urls[].url] | join(","))] | @tsv'
}

# url_of <displayName> <endpointName>
url_of() {
  aspire describe --apphost "$APPHOST_DIR" --format Json \
    | jq -r --arg d "$1" --arg n "$2" '.resources[] | select(.displayName == $d) | .urls[] | select(.name == $n) | .url' \
    | head -1
}

# web_url <dev|production>
web_url() {
  if [[ "$1" == "production" ]]; then url_of bff https; else url_of webfrontend http; fi
}

# server_rejects_anonymous <web>: true when an anonymous GET of /api/v1/users/me through the SPA proxy and the BFF gets
# the 401 of the Server. Only the Server sends a Bearer challenge. The 401 of the CSRF check of the BFF has none.
server_rejects_anonymous() {
  local headers
  headers="$(curl -sk -o /dev/null -D - -H 'X-CSRF: 1' "$1/api/v1/users/me" | tr -d '\r' || true)"
  grep -q '^HTTP/[0-9.]* 401' <<<"$headers" && grep -qi '^www-authenticate:.*bearer' <<<"$headers"
}
