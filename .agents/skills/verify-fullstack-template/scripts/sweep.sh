#!/usr/bin/env bash
# Usage: sweep.sh [recipe.mjs ...]    (default: each recipe in recipes/ and its folders)
set -uo pipefail

S="$(cd "$(dirname "$0")/.." && pwd)"
recipes=("$@")
if (( ! ${#recipes[@]} )); then
  while IFS= read -r recipe; do recipes+=("$recipe"); done < <(find "$S/recipes" -name "*.mjs" | sort)
fi

failed=0
for recipe in "${recipes[@]}"; do
  name="$(basename "$recipe" .mjs)"
  node "$S/scripts/browse.mjs" "$recipe" --label "$name-sweep" --sweep || failed=1
  node "$S/scripts/browse.mjs" "$recipe" --label "$name-sweep-no-web-font" --sweep --no-web-font || failed=1
done
(( failed == 0 )) && echo "sweep: PASSED" || echo "sweep: FAILED"
exit "$failed"
