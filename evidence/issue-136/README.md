# Evidence for issue 136

Screenshots and results of `lever/repro.mjs` on `main` (e146ff2d) and on the head of `fix/issue-136-range-end-focus`.
The lever drives `/filter-demo` in Chromium against the Vite dev server with a mocked BFF, at 320, 768, and 1280 px.
The clock is fixed at 2026-08-15.

Run it from any folder: `node lever/repro.mjs <frontendDir> <viteURL> <outDir>`.
Compare two runs: `node lever/diff.mjs <frontendDir> <dirA> <dirB>`.
