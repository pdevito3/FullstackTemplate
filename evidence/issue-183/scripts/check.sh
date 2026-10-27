#!/bin/bash
# Usage: check.sh <repo> <base> <head|WORKTREE>
U=frontend/src/components/ui
exec python3 -I "$(dirname "$0")/check-moves.py" "$1" "$2" "$3" $U/calendar.tsx $U/calendar-zoom.tsx $U/calendar-presets.ts $U/calendar-layout.ts
