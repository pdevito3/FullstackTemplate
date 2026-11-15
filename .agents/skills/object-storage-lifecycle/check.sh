#!/bin/sh
# Checks the object storage model with TLC. Each config has an expected result: "pass", or the
# rules that break on a known finding. Exit 0 when every config gives its expected result.
# Pass config names (such as LateCommit) to check only those.
set -eu
cd "$(dirname "$0")/model"
mise trust --quiet . >/dev/null
mise install --quiet >/dev/null
jar="$(mise where http:tla2tools)/tla2tools.jar"
java="$(mise which java)"
out=$(mktemp -d)

expected() {
  case $1 in
    ObjectStorage | Liveness | Chart) echo "pass" ;;
    LateCommit | GraceNone | GraceLimit) echo "NoLiveRowWithoutObject" ;;
    SoftDeletedNames) echo "liveness" ;;
    DeletedTenantOrphan) echo "NoObjectLeftInDeletedTenant" ;;
    *) echo "unknown config $1" >&2; exit 2 ;;
  esac
}

# The rules that one TLC run breaks. TLC stops at the first broken rule.
broken() {
  log=$1
  sed -n 's/^Error: Invariant \([A-Za-z0-9_]*\) is violated.*/\1/p' "$log"
  grep -q '^Error: Temporal properties were violated' "$log" && echo "liveness"
  grep -q '^Error: Deadlock reached' "$log" && echo "NoDeadlock"
  true
}

# Runs TLC until no rule breaks, and drops each invariant that breaks from the config of the next run.
result() {
  cfg=$1 dir=$2
  cp "$cfg.cfg" "$dir/run.cfg"
  found=""
  run=0
  while :; do
    run=$((run + 1))
    log="$dir/run-$run.out"
    "$java" -XX:+UseParallelGC -cp "$jar" tlc2.TLC -workers auto -metadir "$dir/states-$run" \
      -config "$dir/run.cfg" ObjectStorage.tla >"$log" 2>&1 || echo "TLC exit code $?" >>"$log"
    sed -n 's/.* \([0-9,]*\) distinct states found.*/\1/p' "$log" | tail -1 >"$dir/states"
    new=$(broken "$log")
    if [ -z "$new" ]; then
      grep -q '^Model checking completed. No error has been found' "$log" || found="$found error"
      break
    fi
    found="$found $new"
    case $new in liveness | NoDeadlock) break ;; esac
    sed -i.bak "s/^\(INVARIANTS* .*\) $new\( .*\)*$/\1\2/" "$dir/run.cfg"
    [ $run -lt 6 ] || { found="$found error"; break; }
  done
  set -- $found
  [ $# -gt 0 ] && echo "$@" | tr ' ' '\n' | sort -u | tr '\n' ' ' | sed 's/ $//' || echo "pass"
}

configs=${*:-"ObjectStorage Liveness Chart LateCommit GraceNone GraceLimit SoftDeletedNames DeletedTenantOrphan"}
status=0
for cfg in $configs; do
  want=$(expected "$cfg")
  mkdir -p "$out/$cfg"
  got=$(result "$cfg" "$out/$cfg")
  states=$(cat "$out/$cfg/states")
  if [ "$got" = "$want" ]; then
    echo "baseline  $cfg: $got ($states states)"
  else
    echo "CHANGED   $cfg: expected [$want], got [$got] ($states states). TLC output: $out/$cfg"
    status=1
  fi
done
exit $status
