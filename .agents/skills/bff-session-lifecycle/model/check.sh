#!/bin/sh
# Checks the BFF session model with the pinned TLC. Each config must give its expected verdict:
# every rule holds, except the findings listed below, and each finding must still break.
# Exit 0: each verdict is as expected. Exit 1: a verdict changed. Read the output of a changed verdict with /statechart.
set -eu
cd "$(dirname "$0")"
mise install --quiet >/dev/null
JAR="$(mise where http:tla2tools)/tla2tools.jar"
OUT="${TMPDIR:-/tmp}/bff-session-lifecycle"
rm -rf "$OUT" && mkdir -p "$OUT"
failed=0

tlc() { # tlc NAME CFG
  mise exec -- java -XX:+UseParallelGC -jar "$JAR" -workers auto -metadir "$OUT/$1.meta" \
    BffSession.tla -config "$2" >"$OUT/$1.out" 2>&1 || true
}

# check CFG FINDING...: the rules of CFG without the findings must hold, and each finding must break.
check() {
  cfg=$1; shift
  name=$(basename "$cfg" .cfg)
  held="$OUT/$name.held.cfg"
  cp "$cfg" "$held"
  for rule in "$@"; do sed -i.bak "/^ *$rule\$/d" "$held"; done
  tlc "$name.held" "$held"
  if grep -q "Model checking completed. No error has been found." "$OUT/$name.held.out"; then
    echo "ok    $name: the other rules hold ($(grep -o '[0-9,]* distinct states found, 0' "$OUT/$name.held.out" | tail -1 | cut -d' ' -f1) states)"
  else
    echo "FAIL  $name: a rule that held breaks now, or TLC stopped. See $OUT/$name.held.out"
    failed=1
  fi
  for rule in "$@"; do
    one="$OUT/$name.$rule.cfg"
    sed -e '/^INVARIANTS/,$d' "$cfg" >"$one"
    printf 'INVARIANTS\n    %s\n' "$rule" >>"$one"
    tlc "$name.$rule" "$one"
    if grep -q "Invariant $rule is violated" "$OUT/$name.$rule.out"; then
      echo "ok    $name: the finding $rule still breaks"
    else
      echo "FAIL  $name: the finding $rule no longer breaks. See $OUT/$name.$rule.out"
      failed=1
    fi
  done
}

check Refresh.cfg
check RefreshLiveness.cfg
check BackChannel.cfg BackChannelLogoutEndsSessions
check BffSessionChart.cfg
exit $failed
