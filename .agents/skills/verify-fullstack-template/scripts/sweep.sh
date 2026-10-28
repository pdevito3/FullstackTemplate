#!/usr/bin/env bash
# Usage: sweep.sh [recipe.mjs ...]    (default: each recipe in recipes/)
set -uo pipefail

S="$(cd "$(dirname "$0")/.." && pwd)"
recipes=("$@")
(( ${#recipes[@]} )) || recipes=("$S"/recipes/*.mjs)

failed=0
for recipe in "${recipes[@]}"; do
  name="$(basename "$recipe" .mjs)"
  node "$S/scripts/browse.mjs" "$recipe" --label "$name-sweep" --sweep || failed=1
  node "$S/scripts/browse.mjs" "$recipe" --label "$name-sweep-no-web-font" --sweep --no-web-font || failed=1
done
(( failed == 0 )) && echo "sweep: PASSED" || echo "sweep: FAILED"
exit "$failed"
