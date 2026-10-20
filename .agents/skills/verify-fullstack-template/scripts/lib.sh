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
