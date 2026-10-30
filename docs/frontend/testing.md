# Frontend tests

The frontend has four test suites. Put a test in the lowest suite that can prove the behavior.

| Suite | Command | Runner | Files | What it proves |
|-------|---------|--------|-------|----------------|
| unit | `pnpm test` | Vitest in Node | `src/**/*.test.ts`, `e2e/*.test.ts` | Pure rules and shapes. No React, no network. |
| component | `pnpm test` | Vitest browser mode in Chromium | `src/**/*.test.tsx` | One component in a real browser. |
| functional | `pnpm test:e2e` | Playwright | `tests/features/**/*.spec.ts` | A real page of the SPA against a mocked BFF. |
| stack | `pnpm test:e2e:stack` | Playwright | `e2e/*.spec.ts` | The login flows against a live Aspire stack and a real IdP. See [authentication.md](../authentication.md). |

CI runs the unit, component, and functional suites on each pull request. CI runs the stack suite when the `changes` job finds a change on the login path: the BFF, the AppHost, the SPA, or the stack suite. See the `bff` filter in `.github/workflows/ci.yml`.

## The component suite

A component test renders one component, or one route with `createMemoryHistory`, in Chromium. A component that loads data gets its answers from `commands.answerRequests`. It answers each request to `/api/*` and `/bff/*` that matches the method, the path, and the query parameters of an answer. A later answer wins, so put the most specific answer last. The body is sent as JSON. To send another type, set `contentType` and give the body as a string. The setup file removes the answers before each test. For example:

```ts
await commands.answerRequests([
  { method: 'GET', path: '/api/v1/users', headers: { 'x-pagination': pagination }, body: [user] },
  { method: 'GET', path: '/api/v1/users', query: { pageNumber: '2' }, headers: { 'x-pagination': page2 }, body: [other] },
])
```

A request to `/api/*` or `/bff/*` with no answer fails the test, and the error names the request. The check runs after the test. A request that starts as the test ends can count for the next test, or for no test. The dev server has no BFF, so it answers such a request with status 502. This is also true for `pnpm dev` when Aspire does not set `BFF_HTTPS` or `BFF_HTTP`.

A component that takes a save function as a prop does not need answers. Give it a `vi.fn()` that resolves, or that rejects with a `ProblemDetailsError`.

A click on an option of a combobox can scroll the page, and a scroll of the page closes the list. Pick an option with `userEvent.keyboard('{ArrowDown}{Enter}')`.

When a component test fails in CI, open the `.vitest/attachments/` folder in the test report artifact of the job. It has the failure screenshot and the Playwright trace of each failed test. The screenshot is at `failure-screenshots/<path of the test file>/<full test name>.png`, with a `-` in place of each character that is not a letter, a digit, or `_`. Vitest 5.0.5 puts only the file name of the test file in that path. So when two files of one name in two folders had a failed test of the same name, one screenshot replaced the other. The patch of `@vitest/browser` in `frontend/patches/` puts the path of the file there. Open the trace at https://trace.playwright.dev. For a test that timed out, the last action with no end is the step that the test waited on. Only CI records the traces, because they make `pnpm test` about 50% slower. In CI that is 16 s more for each job: the median of 8 runs with traces and 8 runs without traces, on one commit, in each of the `api-contract` and `without-examples` jobs. To record them locally, run `CI=1 pnpm test`.
<!-- example:start -->

The workshop example has these component tests: the limits of the contract and the stale-version reload of the edit form ([`refuses a title and a capacity over the limits of the contract`](<../../frontend/src/examples/workshops/components/workshop-edit-form.test.tsx>), [`after a stale save, loads the latest`](<../../frontend/src/examples/workshops/components/workshop-edit-form.test.tsx>)), the venue picker with a pick that is not on the first page ([`shows a picked venue that is not on the first page`](<../../frontend/src/examples/venues/components/venue-picker.test.tsx>)), and the search and the page of the list in the URL ([`puts the page in the URL`](<../../frontend/src/examples/workshops/workshop-list.test.tsx>)).
<!-- example:end -->

## The functional suite

`pnpm test:e2e` starts the Vite dev server on a free port. Then it runs the specs in Chromium. No BFF, Server, or IdP runs. The suite mocks each request to `/api/*` and `/bff/*` in the browser with `page.route`. The hooks, the components, the generated clients, and `apiClient` all run.

```
frontend/
  playwright.config.ts           # the functional suite
  playwright.stack.config.ts     # the stack suite
  tests/
    features/
      home/
        home.locators.ts         # selectors, and nothing else
        home.tasks.ts            # user actions and every wait
        home.spec.ts             # the scenarios
    support/
      api/
        mock-route.ts            # MockRoute and the request recorder
        bff-api.ts               # /bff/user, /bff/login, /bff/logout
        users-api.ts             # one file for each area: the canned responses
      auth/
        actors.ts                # named identities: as.anonymous(), as.user(name)
      builders/
        problem-details-builder.ts  # immutable fluent builders
      fixtures.ts                # `test` with the `actor` and `api` fixtures
```

A test of a shared component, such as the date picker, the overlays, or the toasts, opens `src/test/fixtures/shared-components.html`. This page shows the components in the layout of the app. Do not test a shared component on an example page, because `scripts/remove-examples.cs` deletes the example pages and their tests. Only the Vite dev server serves the page, so it is not in the production build.

### Mocks

A mock is a `MockRoute`: a plain object with a method, a full path, a status, and a body. Each area has one file in `tests/support/api/`. Each method of the file is one answer of the API, and its name states the scenario: `usersApi.currentIs(...)`, `usersApi.currentFails(...)`.

The `api` fixture applies to each test automatically:

- It answers each request from the mocks of the test. A later mock wins over an earlier one, so a test can change an answer in the middle of a scenario.
- It records each request that it answers. Read a request back with `api.last(method, path)` or `api.all(method, path)`.
- It fails the test if the page calls a route that has no mock. The failure message names the route.

A mock matches on the method and the path only. Thus, for each value that the page puts on the wire, write one test that reads the request back. Examples are a query parameter, a header, and a body field.

A list answer has the `X-Pagination` header. Make it with `paginationHeaders` from [`pagination.ts`](../../frontend/tests/support/api/pagination.ts).

An error body must have the shape that ASP.NET sends. Make it with `aProblem()`. `isProblemDetails` accepts a body only if it has `type`, `title`, and `status`.

### Identity

The SPA knows the user only from `GET /bff/user`. The `actor` fixture gives the claims of that answer, so a test never drives a login form. The default actor is anonymous, and `/bff/user` answers 204. To act as a signed-in user, add this line to a `describe` block:

```ts
test.use({ actor: as.user('Bob Smith') })
```

Login and logout leave the SPA. The browser goes to `/bff/login` or `/bff/logout`. Mock these with `bffApi.loginOpens()` and `bffApi.logoutOpens()`, and read the query of the request back.

### Builders

All test data comes from the builders in `tests/support/builders/`:

1. A builder needs no parent. `aProblem()` alone is valid.
2. A builder is immutable. Each method returns a new builder.
3. A builder imports nothing from Playwright or Vitest.
4. The types come from the generated client. A builder derives each dependent value, so the data cannot contradict itself.

Set only the values that the scenario is about. If a scenario needs a value that no builder method sets, add the method to the builder.

`aProblem().staleVersion()` and `aProblem().invalid(errors)` make the 409 of a stale save and the 422 of a validation problem, as the Server sends them.
<!-- example:start -->

The workshop builder gives each status the actions that a caller with `manage_workshops` gets, and `seenByReader()` takes them away: [`MANAGER_ACTIONS`](<../../frontend/tests/support/examples/workshop-builder.ts>). The spec of the workshops walks from the list to the detail and the edit form, and reloads after a stale save: [`opens a workshop from the list`](<../../frontend/tests/features/examples/workshops/workshops.spec.ts>). It reads and shows each start in the zone of the venue, at a fixed clock on which the day at the venue is not the UTC day: [`adds a workshop with the start that the user types`](<../../frontend/tests/features/examples/workshops/workshops.spec.ts>).
<!-- example:end -->

### Locators, tasks, and specs

- Only `*.locators.ts` knows a selector.
- Only `*.tasks.ts` knows a wait. A task that causes a request waits for its response. Arm the wait before the action, and match the method and the exact path.
- A spec has three parts: mock, act through the tasks, assert. Put a blank line between the parts.
- Import `test` and `expect` from `@tests/support/fixtures`, not from `@playwright/test`.
- Do not use `page.waitForTimeout`, retries, or `test.only`.

Check: `pnpm lint` fails when a `*.spec.ts` file calls a selector (`locator`, `getByRole`, and the other `getBy` calls), a `page.waitFor` call, `setTimeout`, or `test.only` (also `test.describe.only`), or imports `test` or `expect` from `@playwright/test`. It fails on `waitForTimeout` in each file of `tests/`. The rules are in `frontend/eslint.config.js`. No check finds a wait in a locators file, and no check reads the three parts of a spec.

### Selectors

Use the first of these that works:

1. A role and an accessible name: `getByRole('button', { name: 'Refresh' })`.
2. A label: `getByLabel('Status')`.
3. A `data-testid`, only if 1 and 2 do not work.

Never use a CSS class, a tag chain, or an `nth` index. If an element has no role or name, give it one in the app. For example, a grid of cards is a `list`, and each card is a `listitem` with its title as its name.

Check: `pnpm lint` fails on `nth`, `first()`, and `last()` in each `.ts` file of `tests/`, also on a helper of a builder with that name. It also fails when an action gets a selector string, such as `page.click('text=Save')`, `page.fill('#name', name)`, or `page.$('.card')`. The rule reads the type of the object, so it fails on each action of a Playwright `Page` or `Frame` with any name, such as `popup.fill('#name', name)` or `page.mainFrame().fill(selector, name)`. A union type, such as `Page | Frame`, counts. So does `waitForSelector` of an `ElementHandle`. So does an action on a value of type `any`: give the value its type, such as `Page` or `Locator`. It also fails on each `$`, `$$`, `$eval`, and `$$eval`. In `e2e/`, the lint fails on a selector string, on `test.only`, and on an action on a value of type `any`. Its message there names the `e2e/idps/*.json` file of the IdP as the place for a selector of a login page. The rules for a position, a wait for a time, and the locators of a feature do not apply there, because the stack suite drives the login pages of the IdPs and Swagger UI. No check finds a CSS class or a tag chain in `locator()`.

### Facts about this app

- A React Aria toast has the role `alertdialog`.
- The queue of toasts renders in a view transition. The suite sets `reducedMotion: 'reduce'`, so each toast renders at once.
- An error toast closes after 6 seconds. `toHaveCount` retries for 5 seconds, so `toHaveCount(1)` can pass when an extra toast closes. To prove that only one toast shows, read `count()` once.
- The queries retry once (`main.tsx`). A failed load sends two requests, and the page shows the error after the second.
- The suite sets the locale `en-US`. For the zone, see [Time zones](#time-zones).

### Time zones

A page that shows a time in the zone of the browser, not of its place, shows another day in a zone far from UTC. So the tests run in zones far from UTC:

| Suite | Zone of the browser | Set in |
|-------|---------------------|--------|
| unit | `Pacific/Pago_Pago` (UTC-11) | `env.TZ` of the `unit` project in `vite.config.ts` |
| component | `Pacific/Kiritimati` (UTC+14) | `contextOptions.timezoneId` of the `browser` project in `vite.config.ts` |
| functional | `UTC`, `Pacific/Kiritimati`, and `Pacific/Pago_Pago` | the projects `utc`, `kiritimati`, and `pago-pago` in `playwright.config.ts` |

Each functional test runs once in each project. To run one project, add `--project=kiritimati`.

- A test of a page with no place sets its clock in the zone of the browser. Use `instantIn(wallClock, browserZone())` from `@tests/support/clock`. Then today is the same date in each project.
<!-- example:start -->
- `openOn` in `filter-demo.tasks.ts` sets the clock of a page with no place.
<!-- example:end -->
- A test of a time of a place gives the place a zone far from the browser, and sets the clock to an instant at which the date of the place is not the date of the browser or the UTC date. For example, at 07:00 UTC on August 16, it is August 15 in `Pacific/Pago_Pago` and August 16 in UTC and in `Pacific/Kiritimati`. Write the instant in UTC, such as `new Date('2026-08-16T07:00:00Z')`, so that it is the same instant in each project.
- A test of a zone with a daylight saving time change, such as a time in a spring gap, sets the zone in its describe with `test.use({ timezoneId: 'America/New_York' })`, and runs only in the `utc` project, because the zone of each project is then the same. `browserZone()` gives the zone of the project, not the zone of `test.use`.
- The tests that check this rule are `date-filter.test.tsx`, `date-picker.test.tsx`, `calendar: marks today and opens on today at the place of the date` in `calendar.test.tsx`, `querykit-converter.test.ts`, and `zoned-time.test.ts`.
<!-- example:start -->
- `a date filter of a place in another zone` in `filter-demo.spec.ts` also checks it. The forecast specs in `weather-forecast.spec.ts` fail in `pago-pago` when the page formats a `DateOnly` as midnight UTC.
<!-- example:end -->

### Add a feature

1. Write a builder for each new type, and an API file for each new area.
2. Write the locators file, the tasks file, and a spec that opens the page.
3. Run `pnpm test:e2e`. Run `pnpm test:e2e --repeat-each=10` before you push, to find a flaky test.
