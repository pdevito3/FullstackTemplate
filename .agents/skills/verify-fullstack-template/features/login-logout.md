# Login and logout

A visitor logs in with the `Login` button. The BFF sends the browser to the IdP and back, then keeps the session in the `__Host-bff` cookie. The home page then shows the name that the Server read from the access token, which proves the full token path: browser cookie, BFF, JWT, and Server. `Logout` ends the BFF session and the IdP session.

## Sub-features

- `auth-anonymous` shows `Login`. `GET /bff/user` is 204, and `GET /api/v1/users/me` is the 401 of the Server.
- `auth-login` goes through `/bff/login`, the login page of the IdP, and `/signin-oidc`, back to `/`.
- `auth-session` shows `Signed in as <name>` and `Logout`. `GET /api/v1/users/me` is 200 with the same name. The session survives a reload.
- `auth-logout` goes through the BFF logout and the IdP logout, back to `/` with `Login`.

## How to get to it (user POV)

- Choose `Login` in the `Your session` card of the home page `/`.
- Choose `Logout` at the same place when logged in.
- Only the home page shows these buttons.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0.
- The browser context is new, so nobody is logged in.

- **Anonymous state.** Open `/`. Run `page.goto('/')` after `waitForResponse` on `/bff/user`. The response is 204 and the button `Login` is visible. `fetch('/api/v1/users/me', { headers: { 'X-CSRF': '1' } })` in the page gives 401.
- **Log in.** Choose `Login`, then sign in at the IdP. Run `const { name } = await login()`. The helper clicks `getByRole('button', { name: 'Login' })`. At ZeroFactor, it clicks the user `[data-testid="user-admin@example.com"]`. At DuendeDemo, it fills `#Input_Username` and `#Input_Password` with `bob` and clicks `button[value="login"]`. The browser returns to the `web` origin, and the button `Logout` is visible. `name` is the `name` claim from `GET /bff/user`.
- **Session.** Read the name. Run ``page.getByText(`Signed in as ${name}`)``. The text is visible. `fetch('/api/v1/users/me', { headers: { 'X-CSRF': '1' } })` in the page gives 200, and its `name` is `name`. `context.cookies()` contains `__Host-bff`.
- **Reload.** Run `page.reload()`. The name is visible again.
- **Log out.** Choose `Logout`. Run `logout()`. The helper clicks `Logout`. ZeroFactor sends the browser back at once. At DuendeDemo, the helper clicks the link `a[href*="signout-callback-oidc"]`. The button `Login` is visible on `/`.
- **Logged-out state.** Run `fetch('/bff/user', { headers: { 'X-CSRF': '1' } })` in the page. The status is 204.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/login-logout.mjs`. The captures `anonymous`, `logged-in`, and `logged-out` show the three states.

## Gotchas

- With DuendeDemo, the IdP is a public server. If it is down, login fails. The doctor examines it.
- The Duende demo server sometimes sends its CSS and scripts very slowly (17 to 29 s were seen). Thus the page `load` event can take more than 30 s. In a new login script, wait for `domcontentloaded` and for `#Input_Username`, never for `load`. `login()` does this.
- Each call to `/bff/*` and `/api/*` needs the header `X-CSRF: 1`. Without it, the BFF answers 401 even with a valid session.
- An anonymous visitor is a normal state. `/bff/user` answers 204, so `network.log` and `console.log` get no line for it.
- The page shows the `name` claim of the access token (`Admin User`, or `Bob Smith` at DuendeDemo), not the user name. A token with no name shows the subject.
- A 401 from `/api/v1/users/me` with a valid BFF session means that the token did not reach the Server. Read the `bff` and `server` logs with the `aspire-telemetry` skill.
- Duende shows a "return to the application" page after logout. If the recipe does not follow its link, the page stays on the IdP.
- `launch.sh` sets the provider, whatever the default in `appsettings.json` is. ZeroFactor is the default. `login()` supports only ZeroFactor and DuendeDemo. Other providers use other forms and users. See `frontend/e2e/idps/` for their selectors.
