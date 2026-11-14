#!/bin/sh
# Model-checks the membership lifecycle with TLC: MC.cfg (safety, widest bounds), Live.cfg (liveness), Chart.cfg
# (the small model that /statechart draws). Exits 1 when a rule breaks. TLC writes its files to a temp folder.
# Usage: .agents/skills/membership-lifecycle/check.sh [CONFIG ...]
set -eu
model=$(cd "$(dirname "$0")/model" && pwd)
cd "$model"
mise install --quiet >/dev/null
jar="$(mise where http:tla2tools)/tla2tools.jar"
[ -f "$jar" ] || jar=$(find "$(mise where http:tla2tools)" -name 'tla2tools*.jar' | head -1)
out=$(mktemp -d "${TMPDIR:-/tmp}/membership-lifecycle.XXXXXX")
status=0
for cfg in "${@:-MC.cfg Live.cfg Chart.cfg}"; do
  for c in $cfg; do
    log="$out/${c%.cfg}.out"
    mise exec -- java -XX:+UseParallelGC -cp "$jar" tlc2.TLC -workers auto -metadir "$out/${c%.cfg}" \
      -config "$c" MC.tla >"$log" 2>&1 || true
    if grep -q "Model checking completed. No error has been found." "$log"; then
      echo "PASS $c: $(grep 'distinct states found' "$log" | tail -1)"
    else
      status=1
      echo "FAIL $c: $(grep -m1 -E '^Error: (Invariant|Temporal|Deadlock)|is violated|^Error:' "$log")"
      echo "     TLC log: $log"
    fi
  done
done
exit $status
