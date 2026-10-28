---
name: writing-tests
description: Picks the test suite for a change and writes the test there. Use when you add or change a test, when you ask which suite proves a behavior, or when you ask when to run `pnpm test:e2e` and when to run `pnpm test:e2e:stack`. Covers the Vitest unit and component tests, the Playwright functional suite against a mocked BFF, the Playwright stack suite against a real IdP, and points to the .NET tests.
---

# Writing tests

Put a test in the lowest suite that can prove the behavior. A lower suite is faster, and its failure points closer to the cause.

## Pick the suite

| The behavior | Suite | Command | Files |
|---|---|---|---|
| A pure rule or a shape, with no React and no network | Frontend unit | `pnpm test` | `frontend/src/**/*.test.ts`, or `frontend/e2e/*.test.ts` for a rule of the stack runner |
| One component in a real browser | Frontend component | `pnpm test` | `frontend/src/**/*.test.tsx` |
| A page of the SPA: what the user sees and what the page sends to the BFF | Functional | `pnpm test:e2e` | `frontend/tests/features/**/*.spec.ts` |
| Login, logout, the session cookie, a token refresh, DPoP, PAR, or the BFF against a real IdP | Stack | `pnpm test:e2e:stack` | `frontend/e2e/*.spec.ts` |
| A domain rule, a handler, or a Server endpoint | .NET | `dotnet test --solution FullstackTemplate.slnx` | `tests/`. See `docs/backend.md`, section "Tests". For test data, use the `test-entity-graph` skill. |
| Two requests that race on the same rows | .NET integration test with `PauseAtQuery` | `dotnet test --solution FullstackTemplate.slnx` | `tests/FullstackTemplate.IntegrationTests/`. See `docs/backend.md#race-tests`. |

Run all frontend commands in `frontend/`.

## When to run each Playwright suite

`pnpm test:e2e` (functional):

- Run it after each change in `frontend/src`. It needs no stack and takes about 10 seconds.
- CI runs it on each pull request, in the `api-contract` job.
- It mocks the BFF. Thus it cannot find a fault in the BFF, the Server, or the IdP.

`pnpm test:e2e:stack` (stack):

- Run it after a change to the BFF, the AppHost, an auth provider, the token check of the Server, `frontend/vite.config.ts`, or the login code of the SPA.
- It needs a running stack. Start the stack and wait for `server`, `bff`, and `webfrontend` first. Use the `aspire-cli` skill.
- It takes some minutes, and it tests the provider of the running stack only. CI runs it for each provider, but only when the `changes` job finds a change on the login path (the BFF, the AppHost, the SPA, or the stack suite). See the `bff` filter in `.github/workflows/ci.yml`.
- For the full procedure and the variables, read the "E2E tests" section of `docs/authentication.md`.

Examples:

| Change | Run |
|---|---|
| A new column on a page, or a new filter | `pnpm test:e2e` |
| A new query hook or an error toast | `pnpm test` and `pnpm test:e2e` |
| A new auth provider, or a change to the cookie or the refresh of the BFF | `pnpm test:e2e:stack` against that provider |
| A change to the `/api` proxy of the Vite dev server | Both Playwright suites |
| A new search on a list | An integration test with `%`, `_`, and `\` in the search. See `docs/backend.md`, section "Tests" |

## Write a functional test

Read `docs/frontend/testing.md` before you write one. Then do these steps:

1. If the page uses a new response type, add a builder in `frontend/tests/support/builders/`. Take the type from `@/api/generated`.
2. If the page calls a new area of the API, add `frontend/tests/support/api/<area>-api.ts`. Name each method after the answer, for example `listReturns` or `createFails`.
3. In `frontend/tests/features/<feature>/`, add or extend three files: `*.locators.ts`, `*.tasks.ts`, and `*.spec.ts`.
4. Mock each call before the action that causes it. For each value that the page sends (a query parameter, a header, or a body field), read the request back with `api.last`.
5. If a test needs a signed-in user, add `test.use({ actor: as.user('Name') })`.
6. Run `pnpm test:e2e --repeat-each=10`. All runs must pass.
7. Make the test fail once on purpose. Remove the code that the test protects, and make sure that the test fails. Then put the code back.

Obey these rules:

- Import `test` and `expect` from `@tests/support/fixtures`.
- Use a role and a name for a selector. If the element has no role or name, give it one in the app.
- Do not use `page.waitForTimeout`, `test.only`, or retries.
- Do not add a default mock. An unmocked call must fail the test.
- To prove that only N of an element show, read `count()` once. `toHaveCount` retries, and it can pass when an element closes.

## Write a stack test

1. Add the test to `frontend/e2e/auth-flows.spec.ts`.
2. If the feature exists only at some providers, read it from `capabilities` or `sessionFeatures` in `frontend/e2e/idps/<name>.json`. Skip the test when the provider does not have the feature, and test the fallback.
3. Start a stack with a provider that has the feature. Run `pnpm test:e2e:stack`.

## Write a component or unit test

- A unit test is `*.test.ts` beside the code. It runs in Node, and Vitest also type-checks it.
- A component test is `*.test.tsx` beside the component. It runs in Chromium with Vitest browser mode. Render with `render` from `vitest-browser-react`. To hold a request, use `commands.holdRequests` from `vitest/browser`.
- A test that needs today sets the time with `vi.useFakeTimers({ toFake: ['Date'] })` and `vi.setSystemTime`, as `calendar.test.tsx` does. Make the time from local parts, for example `new Date(2026, 7, 15, 12)`, so that its date is the same in each time zone of the machine. Do not take today from the UTC date, such as `new Date().toISOString().slice(0, 10)`. From 00:00 UTC to local midnight in the Americas, the UTC date is one day after the local date.
- See `docs/frontend.md`, section "Commands".

## Finish

Run these commands in `frontend/`. All must pass:

```bash
pnpm build
pnpm lint
pnpm test
pnpm test:e2e
```

If you changed the BFF, the AppHost, or the login code, also run `pnpm test:e2e:stack` against a running stack.

If the change adds a command that runs again, takes a lock, or checks a rule over other rows, add a race test.
