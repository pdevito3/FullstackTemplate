#!/usr/bin/env bash
# Runs each recipe two times in a row on the current stack. A recipe passes only when both runs pass and each
# restore puts the database back.
# Usage: run-twice.sh [recipe.mjs ...]    (default: each recipe in recipes/ and its folders)
set -uo pipefail

S="$(cd "$(dirname "$0")/.." && pwd)"
recipes=("$@")
if (( ! ${#recipes[@]} )); then
  while IFS= read -r recipe; do recipes+=("$recipe"); done < <(find "$S/recipes" -name "*.mjs" | sort)
fi

failed=0
summary=()
for recipe in "${recipes[@]}"; do
  name="$(basename "$recipe" .mjs)"
  for run in 1 2; do
    out="$(node "$S/scripts/browse.mjs" "$recipe" --label "$name-run$run")"
    code=$?
    echo "$out"
    dir="$(sed -n 's/.*  evidence: //p' <<<"$out" | tail -1)"
    if [[ -f "$dir/result.json" ]]; then
      line="$(jq -r --arg r "$name" --arg n "$run" \
        '"\($r)\t\($n)\t\(.status)\t\(.data.changes | to_entries | map("\(.key) +\(.value.added) ~\(.value.changed) -\(.value.deleted)") | join(", ") | if . == "" then "no rows changed" else . end)\t\(if .data.restored then "restored" else "NOT RESTORED" end)"' \
        "$dir/result.json")"
    else
      line="$name"$'\t'"$run"$'\t'"failed"$'\t'"no result.json (exit $code)"$'\t'"-"
    fi
    summary+=("$line")
    (( code == 0 )) || failed=1
  done
done

echo
printf 'recipe\trun\tstatus\trows the run wrote\tdatabase\n%s\n' "$(printf '%s\n' "${summary[@]}")" | column -t -s $'\t'
(( failed == 0 )) && echo "run-twice: PASSED" || echo "run-twice: FAILED"
exit "$failed"
