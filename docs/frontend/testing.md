# Frontend tests

The frontend has four test suites. Put a test in the lowest suite that can prove the behavior.

| Suite | Command | Runner | Files | What it proves |
|-------|---------|--------|-------|----------------|
| unit | `pnpm test` | Vitest in Node | `src/**/*.test.ts`, `e2e/*.test.ts` | Pure rules and shapes. No React, no network. |
| component | `pnpm test` | Vitest browser mode in Chromium | `src/**/*.test.tsx` | One component in a real browser. |
| functional | `pnpm test:e2e` | Playwright | `tests/features/**/*.spec.ts` | A real page of the SPA against a mocked BFF. |
| stack | `pnpm test:e2e:stack` | Playwright | `e2e/*.spec.ts` | The login flows against a live Aspire stack and a real IdP. See [authentication.md](../authentication.md). |

CI runs the unit, component, and functional suites on each pull request. CI runs the stack suite when the `changes` job finds a change on the login path: the BFF, the AppHost, the SPA, or the stack suite. See the `bff` filter in `.github/workflows/ci.yml`.

## The functional suite

`pnpm test:e2e` starts the Vite dev server on a free port. Then it runs the specs in Chromium. No BFF, Server, or IdP runs. The suite mocks each request to `/api/*` and `/bff/*` in the browser with `page.route`. The hooks, the components, the generated clients, and `apiClient` all run.

```
frontend/
  playwright.config.ts           # the functional suite
  playwright.stack.config.ts     # the stack suite
  tests/
    features/
      weather/
        weather-forecast.locators.ts   # selectors, and nothing else
        weather-forecast.tasks.ts      # user actions and every wait
        weather-forecast.spec.ts       # the scenarios
    support/
      api/
        mock-route.ts            # MockRoute and the request recorder
        bff-api.ts               # /bff/user, /bff/login, /bff/logout
        weather-api.ts           # one file for each area: the canned responses
      auth/
        actors.ts                # named identities: as.anonymous(), as.user(name)
      builders/
        forecast-builder.ts      # immutable fluent builders
        problem-details-builder.ts
      fixtures.ts                # `test` with the `actor` and `api` fixtures
```

### Mocks

A mock is a `MockRoute`: a plain object with a method, a full path, a status, and a body. Each area has one file in `tests/support/api/`. Each method of the file is one answer of the API, and its name states the scenario: `weatherApi.listReturns(...)`, `weatherApi.listFails(...)`.

The `api` fixture applies to each test automatically:

- It answers each request from the mocks of the test. A later mock wins over an earlier one, so a test can change an answer in the middle of a scenario.
- It records each request that it answers. Read a request back with `api.last(method, path)` or `api.all(method, path)`.
- It fails the test if the page calls a route that has no mock. The failure message names the route.

A mock matches on the method and the path only. Thus, for each value that the page puts on the wire, write one test that reads the request back. Examples are a query parameter, a header, and a body field.

An error body must have the shape that ASP.NET sends. Make it with `aProblem()`. `isProblemDetails` accepts a body only if it has `type`, `title`, and `status`.

### Identity

The SPA knows the user only from `GET /bff/user`. The `actor` fixture gives the claims of that answer, so a test never drives a login form. The default actor is anonymous, and `/bff/user` answers 401. To act as a signed-in user, add this line to a `describe` block:

```ts
test.use({ actor: as.user('Bob Smith') })
```

Login and logout leave the SPA. The browser goes to `/bff/login` or `/bff/logout`. Mock these with `bffApi.loginOpens()` and `bffApi.logoutOpens()`, and read the query of the request back.

### Builders

All test data comes from the builders in `tests/support/builders/`:

1. A builder needs no parent. `aForecast()` alone is valid.
2. A builder is immutable. Each method returns a new builder.
3. A builder imports nothing from Playwright or Vitest.
4. The types come from the generated client. A builder derives a dependent value (for example `temperatureF` from `temperatureC`), so the data cannot contradict itself.

Set only the values that the scenario is about. If a scenario needs a value that no builder method sets, add the method to the builder.

### Locators, tasks, and specs

- Only `*.locators.ts` knows a selector.
- Only `*.tasks.ts` knows a wait. A task that causes a request waits for its response. Arm the wait before the action, and match the method and the exact path.
- A spec has three parts: mock, act through the tasks, assert. Put a blank line between the parts.
- Import `test` and `expect` from `@tests/support/fixtures`, not from `@playwright/test`.
- Do not use `page.waitForTimeout`, retries, or `test.only`.

### Selectors

Use the first of these that works:

1. A role and an accessible name: `getByRole('button', { name: 'Refresh' })`.
2. A label: `getByLabel('Status')`.
3. A `data-testid`, only if 1 and 2 do not work.

Never use a CSS class, a tag chain, or an `nth` index. If an element has no role or name, give it one in the app. For example, the forecast grid is a `list`, and each day is a `listitem` with the date as its name.

### Facts about this app

- A React Aria toast has the role `alertdialog`.
- The queue of toasts renders in a view transition. The suite sets `reducedMotion: 'reduce'`, so each toast renders at once.
- An error toast closes after 6 seconds. `toHaveCount` retries for 5 seconds, so `toHaveCount(1)` can pass when an extra toast closes. To prove that only one toast shows, read `count()` once.
- The queries retry once (`main.tsx`). A failed load sends two requests, and the page shows the error after the second.
- The page formats dates with the locale and the time zone of the browser. The suite sets `en-US` and `UTC`.

### Add a feature

1. Write a builder for each new type, and an API file for each new area.
2. Write the locators file, the tasks file, and a spec that opens the page.
3. Run `pnpm test:e2e`. Run `pnpm test:e2e --repeat-each=10` before you push, to find a flaky test.
