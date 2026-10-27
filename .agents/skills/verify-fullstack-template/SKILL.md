---
name: verify-fullstack-template
description: Launches the FullstackTemplate stack (Aspire, server, BFF, React SPA) in isolation, drives the web UI in a real Chromium as a user does, and saves screenshots, ARIA snapshots, traces, and network logs as proof. Use to prove that a change works in the real app, to reproduce a bug end to end before a fix, or to check a feature in the feature map.
---

# Verify FullstackTemplate

The primary surface is the React SPA in a browser. All browser traffic goes through the Vite dev server to the BFF (`/bff/*`, `/api/*`, `/signin-oidc`). The BFF holds the tokens and calls the server with a JWT. Login uses ZeroFactor by default. ZeroFactor is [0factor](https://github.com/c4l3b/0factor), a mock OIDC provider that runs in the stack. Its login page lists the users (`admin@example.com` and `user@example.com`), and one click signs in. To test an interaction with a real IdP, start the stack with `--auth-provider <Name>`. `login()` supports `ZeroFactor` and `DuendeDemo`. For another provider, drive its login form from `frontend/e2e/idps/<name>.json`.

Other surfaces, not in the feature map yet:

- Swagger UI on the server at `<server https URL>/swagger`. The Playwright spec `frontend/e2e/auth-flows.spec.ts` drives it. This spec runs against the provider of the stack, usually Keycloak. To run it, start a stack with `AuthProvider=Keycloak` (not `launch.sh`), and run `pnpm test:e2e:stack` in `frontend`. See the "E2E tests" section of `docs/authentication.md`.
- The JSON API (`/api/v1/users`, `/api/v1/tenants`). It has no UI. Call it through the BFF from a logged-in page with `fetch` and the `X-CSRF: 1` header.

All helpers are in `.agents/skills/verify-fullstack-template/scripts/`. Run them from the checkout root. Below, `$S` means that directory:

```bash
S=.agents/skills/verify-fullstack-template
```

## Isolation

- `launch.sh` always starts an `--isolated` stack: random ports, an empty database, and a copy of the user secrets. A stack in another worktree does not conflict.
- Each checkout can run one stack for its AppHost path. If a stack runs for this checkout and this skill did not start it, `launch.sh` stops with an error. Do not drive that stack and do not stop it. It belongs to the user. Ask the user, or work in a git worktree.
- Each recipe run gets a new Chromium context. No cookies or `localStorage` carry over between recipes.
- If you use `--auth-provider DuendeDemo`, the IdP is shared and public. Use only the demo users. Do not change data on it.

## Launch

1. Start the stack. The command blocks until `server`, `bff`, and `webfrontend` are healthy and `/api/v1/weather` answers 200 through Vite. A cold start takes about 1 to 3 minutes.

   ```bash
   $S/scripts/launch.sh            # or: $S/scripts/launch.sh <RUN_ID>
   ```

   The stack uses ZeroFactor. To test an interaction with a real IdP, give the provider:

   ```bash
   $S/scripts/launch.sh --auth-provider DuendeDemo
   ```

2. Read the last lines of the output. They give `RUN_DIR`, the `authProvider`, the `web` URL (the app), the `server` URL, and the `dashboard` URL.

The stack is ready when the output ends with `launch: ready.` and the URLs. The run state is in `artifacts/verify/<RUN_ID>/stack.json`. The file `artifacts/verify/.current` holds the RUN_ID of the live stack.

If a resource fails to start or stops, `launch.sh` stops at once and prints the last log lines of that resource. If `launch.sh` fails, run `$S/scripts/cleanup.sh` before you try again. For more log lines, run `dotnet aspire logs <resource> -n 100 --apphost FullstackTemplate.AppHost` (see the `aspire-cli` skill).

If a container fails with `bind source path does not exist` and the folder is on disk, git probably deleted and wrote the folder again, for example in a branch switch. Docker Desktop can keep an old view of the path for some minutes. Make sure that `docker run --rm -v <path>:/x alpine ls /x` lists the files. Then run `cleanup.sh` and `launch.sh` again.

## Doctor

Run the doctor first, and again each time something looks wrong. It is read-only.

```bash
$S/scripts/doctor.sh
```

Each line starts with `ok` or `FAIL`. Exit code 0 means the stack is worth driving. The doctor examines these items:

- The current run and its `stack.json` exist.
- The AppHost pid of this checkout is the pid that `launch.sh` recorded. Thus the stack is ours.
- Each resource is `Running` and `Healthy`.
- The web URL is unchanged, `/api/v1/weather` is 200, `/bff/user` is 204 or 200, and the server `/health` is 200.
- If the stack uses `DuendeDemo`, the Duende demo IdP is reachable. If it is not, no login recipe can pass. A local provider, such as ZeroFactor, is a resource of the stack, so the health check above examines it.
- `HEAD` is the commit of the launch. If it moved, the doctor prints `warn`. Frontend code hot-reloads. For server or BFF code, run `dotnet aspire resource server rebuild --apphost FullstackTemplate.AppHost` (or `bff`).

## Drive

The harness is `scripts/browse.mjs`. It loads Playwright from `frontend/node_modules`, opens Chromium at the `web` URL, runs one recipe, and saves the evidence.

```bash
node $S/scripts/browse.mjs $S/recipes/<feature>.mjs [--headed] [--label <name>]
```

The command prints `ok` or `FAIL` for each step, then `PASSED` or `FAILED` and the evidence path. The exit code is 0 only when the recipe does not throw, the page has no uncaught error or console error, and the recipe has no unhandled promise rejection. A step error that the recipe catches logs `FAIL` but does not fail the run.

An uncaught error in the page (a Playwright `pageerror`) fails the run, even if all steps pass. A browser console error also fails the run, because an error boundary catches a render crash and only logs it. The one exception is `Failed to load resource`: `network.log` records those responses. After the `FAILED` line, the command prints each error on a line that starts with `[pageerror]`. A recipe does not need its own page-error check.

An unhandled promise rejection in the recipe also fails the run. An example is a `page.waitForResponse` that the recipe did not await and that never got its response. `steps.log` records each rejection on a line that starts with `unhandled rejection:`. If a step fails while such a promise waits, the run still writes `result.json` with the error of the step.

To prove a feature in the map, run its recipe:

```bash
for r in weather-forecast login-logout secure-weather navigation-and-theme filter-builder components-showcase; do
  node $S/scripts/browse.mjs $S/recipes/$r.mjs
done
```

To prove a new change, write a new recipe in the scratch folder of your run (for example `artifacts/verify/<RUN_ID>/recipes/my-change.mjs`). A recipe is an ES module with a default export. `browse.mjs` gives it these values:

| Value | What it is |
|---|---|
| `page`, `context` | The Playwright page and its browser context. `page.goto('/')` uses the `web` URL. |
| `expect` | The Playwright `expect` with web-first assertions. |
| `step(name, fn)` | Runs `fn` and logs `ok` or `FAIL` with the name. Put each user action and its assertion in one step. If `fn` fails, the step captures the page as `failure` before the error leaves the step, so a `finally` block of the recipe (for example, a `logout()`) does not change the capture. This capture shows only the viewport, so that an open overlay stays open. A step error that the recipe catches also makes this capture. |
| `capture(name, { fullPage })` | Waits for `settle()`, then saves a screenshot and an ARIA snapshot. The screenshot is full-page by default. For an open overlay, give `{ fullPage: false }`, because the resize for a full-page screenshot closes a Drawer. In a full-page screenshot of a scrolled page, the fixed sidebar shows at the scroll offset. This is normal. |
| `settle()` | Waits two frames, then waits for all finite animations to end. Call it before a click in an animated overlay. Playwright retries a click on a moving element with a scroll, and React Aria closes a popover on scroll. |
| `login(user)` | Logs in through the real BFF and the IdP of the stack. It returns `{ name }`, the `name` claim of the session. Compare the screen with this name. Without `user`, the first user of the IdP signs in: `admin@example.com` (`Admin User`) for ZeroFactor, `bob` (`Bob Smith`) for DuendeDemo. The other users are `user@example.com` and `alice`. |
| `logout()` | Logs out. For DuendeDemo, it follows the return link of Duende. |
| `stack` | The content of `stack.json` (`stack.urls.web`, `stack.urls.server`, and more). |

Minimal recipe:

```js
export default async ({ page, step, capture, expect }) => {
  await step('open About', async () => {
    await page.goto('/')
    await page.getByRole('link', { name: 'About' }).click()
    await expect(page.getByRole('navigation', { name: 'breadcrumb' })).toHaveText('About')
  })
  await capture('about')
}
```

Selector rules:

- Use `getByRole` with the accessible name. Copy the names from the ARIA snapshots (`*.aria.txt`) and from the feature files. Use CSS selectors only where the feature files name them.
- Scope a name that occurs twice. For example, `Add Filter` is a toolbar button and also the submit button of the filter dialog. Use `page.getByRole('dialog', ...)` for the second one.
- The ARIA snapshot joins the text of adjacent elements with spaces. `getByText` does not. If a `getByText` copied from a snapshot finds nothing, match the element with a pattern, or use a `data-slot` handle (`[data-slot="badge"]`, `[data-slot="card"]`).
- Wait for a state with `expect(...)`, not with a fixed sleep.
- To get the API data that the page shows, use `page.waitForResponse` before the action that sends the request.
- To read the ARIA tree of a page that you do not know, call `capture('look')` and read the `.aria.txt` file. Then write the next step.

For interactive exploration, the Playwright MCP server in `.mcp.json` also works. Give it the `web` URL from `stack.json`. Put the final proof in a recipe, because only a recipe gives a repeatable result and saves the evidence in the run directory.

## Evidence

Each recipe run writes to `artifacts/verify/<RUN_ID>/<label>/`. The label is the recipe file name, or the value of `--label`. If that directory exists, the run writes to `<label>-2/`, then `<label>-3/`, and so on. The output line `evidence:` gives the directory.

| File | Content |
|---|---|
| `steps.log` | Each step with `ok` or `FAIL` and its duration. Each capture with its URL. |
| `NN-<name>.png` | A full-page screenshot. |
| `NN-<name>.aria.txt` | The ARIA snapshot of the page, with the URL on the first line. |
| `console.log` | Browser console errors and warnings, and page errors. Each page error starts with `[pageerror]`. |
| `network.log` | Each response with status 400 or more. |
| `trace.zip` | The Playwright trace. Open it with `pnpm --dir frontend exec playwright show-trace <path>`. |
| `result.json` | `passed` or `failed`, with the error message. `pageErrors` lists the message of each page error and each console error. One entry or more makes the status `failed`. |
| `NN-failure.png` | A capture of the page at the time of the failure. A failed step makes the capture before the cleanup of the recipe runs. A failure outside a step gets its capture after the recipe ends. |

`artifacts/` is in `.gitignore`. The evidence stays after cleanup.

Proof standards:

- Drive the real user path: click the buttons and links that a user clicks. Do not set React state, call internal functions, or use test-only endpoints.
- Capture the action and the result. A recipe needs a capture before and after the change of state, not only the last screen.
- Prove side effects next to the visible result. Examples: the HTTP status and the JSON of the API response, the `__Host-bff` cookie, the `ui-theme` value in `localStorage`.
- Compare the screen with the data. For example, each weather card must show the `summary` and the temperature of the API row, not only "5 cards".
- Read `console.log` and `network.log` after each run. These lines are normal: the 401 lines from the anonymous probes that a recipe makes on purpose, and, with DuendeDemo, the iframe `sandbox` warning from the Duende pages. Report each other error, even if the recipe passed.
- Look at the screenshots. If something looks wrong (clipped, misaligned, unreadable in dark mode), report it, even if it is not part of your change.
- Do not mock the app. ZeroFactor is a mock IdP, but it gives real OIDC tokens. The BFF and the server validate them as they validate the tokens of any other provider. The Duende demo IdP is the only external system.

## Cleanup

Stop the stack before you finish, also after a failed attempt:

```bash
$S/scripts/cleanup.sh
```

The script stops only the AppHost whose pid `launch.sh` recorded, and deletes `artifacts/verify/.current`. It never stops a stack that it did not start. It does not delete `artifacts/verify/<RUN_ID>/`. Cleanup is complete when the script prints `cleanup: stopped.` and `dotnet aspire ps` does not show this checkout.

Do not kill processes by name (`dotnet`, `node`, `vite`). Other worktrees and the user run the same processes.

## Feature map

The feature map is in `features/`. Read `features/README.md` first. Each feature file gives the user entry points, the recipe, and the observable proof. When a change touches a mapped feature, prove each entry point that the file lists. When you add a user-facing feature, add a feature file and a recipe for it.
