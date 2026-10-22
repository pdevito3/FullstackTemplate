# Secure weather

A logged-in user can change the forecast source from the public API to the secure API. The secure API needs the JWT that the BFF sends to the server. Its rows contain the name of the user, and each card shows it in a green badge. This feature proves the full token path: browser cookie, BFF, JWT, and server.

## Sub-features

- `secure-hidden` hides the API mode button for an anonymous visitor. The BFF answers 401 for `GET /api/v1/weather/secure`.
- `secure-on` changes `Public API` to `Secure API` and loads `GET /api/v1/weather/secure`.
- `secure-badges` shows `Showing authenticated data` and a badge with the name of the user on each card.
- `secure-off` changes back to `Public API`. The badges go away.

## How to get to it (user POV)

- Log in on the home page `/`, then choose `Public API` in the controls card.
- The button is not there when nobody is logged in.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0.
- The browser context is new, so nobody is logged in.

- **Anonymous state.** Open `/`. Run `page.goto('/')`. `getByRole('button', { name: 'Public API' })` has count 0. `fetch('/api/v1/weather/secure', { headers: { 'X-CSRF': '1' } })` in the page gives 401.
- **Log in.** Run `const { name } = await login()`. The button `Public API` is visible.
- **Secure mode.** Choose `Public API`. Run `page.getByRole('button', { name: 'Public API' }).click()` after `waitForResponse` on `/api/v1/weather/secure`. The response is 200 and each row has `requestedBy` = `name`. The button name changes to `Secure API`.
- **Badges.** Read the cards. Run `page.getByText('Showing authenticated data')` and `page.locator('[data-slot="badge"]').filter({ hasText: name })`. The status text is visible and the badge count is 5. Each `${row.temperatureF}°` of the secure response is visible.
- **Public mode again.** Choose `Secure API`. Run `page.getByRole('button', { name: 'Secure API' }).click()`. The button is `Public API` again. `Showing authenticated data` and the badges have count 0.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/secure-weather.mjs`. The captures `public-mode` and `secure-mode` show the two states.

## Gotchas

- The greeting `Welcome, <name>` has the name in its own element. Thus `getByText(name, { exact: true })` finds 6 elements, not 5. Count `[data-slot="badge"]` elements.
- The secure request has no retry. One 401 shows the error box at once.
- The mode is local React state. A reload sets it back to `Public API`.
- `requestedBy` comes from the `name` claim of the access token. It is the same name that `login()` returns.
- A 401 from the secure API with a valid BFF session means that the token did not reach the server. Read the `bff` and `server` logs with the `aspire-telemetry` skill.
