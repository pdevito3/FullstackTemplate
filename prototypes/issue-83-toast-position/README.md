# Prototype: toast region position (issue #83)

Throwaway. This branch is not for merge. It keeps the prototype that chose the toast position for #83.

`frontend/src/components/toaster.tsx` reads `localStorage['prototype-toast-variant']` and places the region in one of three positions:

- `top-center`: the position before the change, `top-4`, centered.
- `bottom`: bottom right at `sm` and wider. Full width at the bottom on a phone. The newest toast is nearest the bottom edge, and it enters from below.
- `under-header`: centered, 8 px below the 64 px header.

The recipes in `recipes/` run with `.agents/skills/verify-fullstack-template/scripts/browse.mjs` against an isolated stack.

- `shots/`: each variant at 1280x800 and 390x844, light and dark, one and three toasts. `metrics.json` has the region box, the header overlap, the stack order, and the enter keyframes.
- `overlays/`: two toasts over an open sheet, drawer, or dialog. `overlays.json` lists the overlay buttons that the toasts cover.
- `home/`: three toasts on the home page.
- `coverage.json`: the page controls that three toasts cover, for each route, viewport, and sign-in state.

Result: `bottom` covers no header control and no page control on any route. `under-header` covers `Login` or `Logout`, the unit toggle, and `Refresh` on the home page. `bottom` covers the primary action of the phone bottom-sheet dialog while a toast shows.

- `final/`: the shipped position from the PR branch, from the live recipe `toast-position-live.mjs`.
