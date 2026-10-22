# Login and logout

A visitor logs in with the `Login` button. The BFF sends the browser to the IdP and back, then keeps the session in the `__Host-bff` cookie. The page then greets the user by name. `Logout` ends the BFF session and the IdP session.

## Sub-features

- `auth-anonymous` shows `Login`, and `GET /bff/user` is 401.
- `auth-login` goes through `/bff/login`, the login page of the IdP, and `/signin-oidc`, back to `/`.
- `auth-session` shows `Welcome, <name>` and `Logout`. The session survives a reload.
- `auth-logout` goes through the BFF logout and the IdP logout, back to `/` with `Login`.

## How to get to it (user POV)

- Choose `Login` at the top right of the home page `/`.
- Choose `Logout` at the same place when logged in.
- Only the home page shows these buttons.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0.
- The browser context is new, so nobody is logged in.

- **Anonymous state.** Open `/`. Run `page.goto('/')` after `waitForResponse` on `/bff/user`. The response is 401 and the button `Login` is visible.
- **Log in.** Choose `Login`, then sign in at the IdP. Run `const { name } = await login()`. The helper clicks `getByRole('button', { name: 'Login' })`. At ZeroFactor, it clicks the user `[data-testid="user-admin@example.com"]`. At DuendeDemo, it fills `#Input_Username` and `#Input_Password` with `bob` and clicks `button[value="login"]`. The browser returns to the `web` origin, and the button `Logout` is visible. `name` is the `name` claim from `GET /bff/user`.
- **Session.** Read the greeting. Run ``page.getByText(`Welcome, ${name}`)``. The greeting is visible. `context.cookies()` contains `__Host-bff`.
- **Reload.** Run `page.reload()`. The greeting is visible again.
- **Log out.** Choose `Logout`. Run `logout()`. The helper clicks `Logout`. ZeroFactor sends the browser back at once. At DuendeDemo, the helper clicks the link `a[href*="signout-callback-oidc"]`. The button `Login` is visible on `/`.
- **Logged-out state.** Run `fetch('/bff/user', { headers: { 'X-CSRF': '1' } })` in the page. The status is 401.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/login-logout.mjs`. The captures `anonymous`, `logged-in`, and `logged-out` show the three states.

## Gotchas

- With DuendeDemo, the IdP is a public server. If it is down, login fails. The doctor examines it.
- The Duende demo server sometimes sends its CSS and scripts very slowly (17 to 29 s were seen). Thus the page `load` event can take more than 30 s. In a new login script, wait for `domcontentloaded` and for `#Input_Username`, never for `load`. `login()` does this.
- Each call to `/bff/*` and `/api/*` needs the header `X-CSRF: 1`. Without it, the BFF answers 401 even with a valid session.
- Each anonymous page load logs a `401 /bff/user` line in `network.log` and `console.log`. This is normal.
- The greeting shows the `name` claim (`Admin User`, or `Bob Smith` at DuendeDemo), not the user name.
- Duende shows a "return to the application" page after logout. If the recipe does not follow its link, the page stays on the IdP.
- `launch.sh` sets the provider, whatever the default in `appsettings.json` is. ZeroFactor is the default. `login()` supports only ZeroFactor and DuendeDemo. Other providers use other forms and users. See `frontend/e2e/idps/` for their selectors.
