# FullstackTemplate verification map

This directory is the maintained source for the proof of user-facing behavior in the FullstackTemplate SPA. Read this index before you drive the app. Then use the matching feature file as the recipe.

## Baseline preconditions

- `scripts/launch.sh` started an isolated stack for this checkout, and `scripts/doctor.sh` exits with 0.
- The browser is a new Chromium context from `scripts/browse.mjs`. Nobody is logged in. `localStorage` is empty, so the theme is `system`.
- The database is empty. No feature in this map reads or writes app data. The weather endpoints make random data at each request.
- The IdP is ZeroFactor, unless `launch.sh` got `--auth-provider`. The ZeroFactor users are `admin@example.com` (name `Admin User`) and `user@example.com` (name `Test User`).
- Never drive a stack that the current verification run did not start.

## Driving conventions

- Run each recipe with `node .agents/skills/verify-fullstack-template/scripts/browse.mjs .agents/skills/verify-fullstack-template/recipes/<feature>.mjs`.
- Each recipe starts from the baseline state. A recipe that needs a login calls `login()` itself and uses the `name` that it returns. Do not write a fixed user name in a recipe, because each IdP has other users.
- Use ARIA roles and accessible names. Use CSS only where a feature file names it.
- Copy the quoted names and commands exactly.
- The weather data is random. Assert that the screen matches the API response of the same run, never a fixed value.

## Proof and skip reporting

- Capture the user action and the resulting state, not only the last screen.
- UI proof is a screenshot and an ARIA snapshot from `capture()`, with the URL in the `.aria.txt` file.
- Proof of a side effect is a second, read-only view: the API response, the BFF session, or `localStorage`.
- Record the feature ID and the entry point for each artifact. The `steps.log` step names do this.
- If a path is not reachable, report the command and the precondition that failed. For example, the Duende demo IdP is down.
- Do not report an entry point as verified when you used a different entry point.

## Feature entry contract

Each feature file starts with an H1 title and one paragraph about the user-visible behavior. It then has exactly four H2 sections in this order:

1. `Sub-features` gives short IDs, one line for each behavior.
2. `How to get to it (user POV)` gives each user entry point.
3. `Driving it with browse.mjs` starts with `Preconditions:`. Then labeled bullets give each user action, the exact Playwright call, and the observable result.
4. `Gotchas` gives the traps that can waste or invalidate a verification run.

Keep implementation details out of the map. Name only user paths, stable handles, required state, commands, and observable proof.

## Features

- [Weather forecast](./weather-forecast.md) covers the public forecast cards, the °F/°C toggle, and Refresh.
- [Login and logout](./login-logout.md) covers the BFF login through the IdP of the stack, the session, and logout.
- [Secure weather](./secure-weather.md) covers the Public API/Secure API toggle and the authenticated forecast.
- [Navigation and theme](./navigation-and-theme.md) covers the sidebar links, the Demos submenu, the breadcrumb, sidebar collapse, and the light/dark/system theme.
- [Filter builder](./filter-builder.md) covers the demo at `/filter-demo`: add, edit, and remove filters, the number filter, multiselect filters with more than one value, grouping, presets, AND/OR, and Clear.
- [Components showcase](./components-showcase.md) covers the Component Library at `/components`: form controls and their layout, the focus on load, the active sidebar items, overlays, toasts, and the collapsible.

## Not mapped yet

- The boolean filter dialog and the add path of the date filter dialog on the filter builder. The filter builder map covers only the date edit dialog.
- Swagger UI on the server (`/swagger`). The Playwright spec `frontend/e2e/auth-flows.spec.ts` covers it.
- The Users and Tenants JSON API. It has no UI.
