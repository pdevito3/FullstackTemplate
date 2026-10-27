"""Move-only split of frontend/src/components/ui/calendar.tsx (main 5ee10a9f, 1034 lines).

Usage: python3 -I split.py <ui-dir>. Cuts whole line blocks out of calendar.tsx into new files, in their original
order, byte for byte. It adds no line and changes no line. Refuses to run if calendar.tsx is not the 1034-line file.
"""
import hashlib, pathlib, sys

ui = pathlib.Path(sys.argv[1])
src = ui / "calendar.tsx"
data = src.read_bytes()
assert hashlib.sha256(data).hexdigest().startswith(sys.argv[2]), "calendar.tsx is not the expected base file"
lines = data.splitlines(keepends=True)
assert len(lines) == 1034, len(lines)

# 1-based inclusive line ranges. Each block keeps its trailing blank line.
moves = {
    "calendar-zoom.tsx": [(46, 118), (225, 550), (607, 614)],
    "calendar-presets.ts": [(119, 174)],
    "calendar-layout.ts": [(175, 202)],
}

taken = set()
for name, ranges in moves.items():
    out = []
    for a, b in ranges:
        for n in range(a, b + 1):
            assert n not in taken, n
            taken.add(n)
            out.append(lines[n - 1])
    target = ui / name
    assert not target.exists(), target
    target.write_bytes(b"".join(out))

src.write_bytes(b"".join(l for n, l in enumerate(lines, 1) if n not in taken))
