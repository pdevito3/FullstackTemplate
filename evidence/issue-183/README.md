# Evidence for issue 183 (split calendar.tsx)

Not for merge.

## Scripts

- `scripts/split.py` made the move-only commit. It cuts whole blocks of lines out of `calendar.tsx` into the new files. It adds no line and changes no line.
- `scripts/check-moves.py` (through `scripts/check.sh`) proves that a diff only moves lines. Each removed line must be added again, and each new file must keep the source order. Run it from the repo root:

  ```bash
  scripts/check.sh . 5ee10a9f <move-commit>   # exit 0, "unmatched: 0"
  scripts/check.sh . 5ee10a9f <head>          # lists only the import and export lines
  ```

  Two seeded faults make it fail: a changed character in a moved line, and two lines swapped inside a block.
- `scripts/shots.spec.ts` takes the screenshots. Copy it to `frontend/tests/features/zz-shots/`, then run `SHOTS_DIR=<dir> pnpm exec playwright test tests/features/zz-shots --workers=4`. It covers the Created Date filter control (On, Between, the month and year pickers, the end-before-start and short-year errors) and the two pickers of the components page, at 1280 px and 320 px, light and dark.
- `scripts/match-any.mjs` passes when each screenshot of the head has the exact pixels of the same screenshot in a run on main.

## Screenshots

`before/` is main at 5ee10a9f. `after/` is the head. Each pair has the same pixels.
