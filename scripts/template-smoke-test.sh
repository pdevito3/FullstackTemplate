#!/usr/bin/env bash
# Packs the template, then creates a project from the package.
# Fails when the package or the new project has a local file that git ignores.
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
work_dir="$(mktemp -d)"
canary="$repo_root/FullstackTemplate.Server/logs/template-smoke-canary.log"
canary_dir_created=false

cleanup() {
  rm -f "$canary"
  if [ "$canary_dir_created" = true ]; then rmdir "$(dirname "$canary")" 2>/dev/null || true; fi
  rm -rf "$work_dir"
}
trap cleanup EXIT

# An ignored log file, like the ones that a local run writes. It must not get into the package.
if [ ! -d "$(dirname "$canary")" ]; then
  mkdir -p "$(dirname "$canary")"
  canary_dir_created=true
fi
echo "smoke canary" > "$canary"

dotnet pack "$repo_root/template.csproj" -o "$work_dir/nupkg" -v q
package="$(ls "$work_dir"/nupkg/*.nupkg)"

forbidden='(^|/)(bin|obj[^/]*|logs|node_modules)/|\.log$|(^|/)\.git$|settings\.local\.json$|(^|/)\.env$'

package_files="$(unzip -Z1 "$package")"
if echo "$package_files" | grep -E "$forbidden"; then
  echo "FAIL: the package has the files above. Only files that git tracks belong in the package." >&2
  exit 1
fi

hive="$work_dir/hive"
dotnet new install "$package" --debug:custom-hive "$hive" > /dev/null
# Exit code 104 means that a post action did not run. The script skips them, so 104 is expected.
dotnet new fullstack-aspire -n SmokeApp -o "$work_dir/SmokeApp" --no-restore --allow-scripts no --debug:custom-hive "$hive" > /dev/null 2>&1 \
  || [ $? -eq 104 ]

project_files="$(cd "$work_dir/SmokeApp" && find . -type f -o -type l | sed 's|^\./||')"
if echo "$project_files" | grep -E "$forbidden|^\.playwright-mcp/"; then
  echo "FAIL: the new project has the files above." >&2
  exit 1
fi
if [ ! -f "$work_dir/SmokeApp/SmokeApp.slnx" ]; then
  echo "FAIL: the new project has no SmokeApp.slnx." >&2
  exit 1
fi

echo "PASS: $(echo "$package_files" | wc -l | tr -d ' ') package entries, $(echo "$project_files" | wc -l | tr -d ' ') project files."
