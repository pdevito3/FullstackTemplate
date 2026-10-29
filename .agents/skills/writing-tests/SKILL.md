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
| Who can call a Server endpoint: the 403 for a caller with no permission, and the 404 for a record of another tenant | .NET functional sweep, no new test | `dotnet test --solution FullstackTemplate.slnx` | `tests/FullstackTemplate.FunctionalTests/FunctionalTests/Access/EndpointAccessSweepTests.cs`. See "Access of a Server endpoint" below. |
| A domain rule, a handler, or a Server endpoint | .NET | `dotnet test --solution FullstackTemplate.slnx` | `tests/`. See `docs/backend.md`, section "Tests". For test data, use the `test-entity-graph` skill. |
| Two requests that race on the same rows | .NET integration test with `PauseAtQuery` | `dotnet test --solution FullstackTemplate.slnx` | `tests/FullstackTemplate.IntegrationTests/`. See `docs/backend.md#race-tests`. |

Run all frontend commands in `frontend/`.

## Access of a Server endpoint

`EndpointAccessSweepTests` finds each HTTP endpoint, MCP tool, and gRPC method of the Server. It sends each one these calls:

- A call with no user, with unknown IDs. The answer must be 401, or `Unauthenticated` on gRPC. MCP gets no such call, because `/mcp` refuses a call with no token before a tool runs, and `TenantMcpToolTests` checks that 401.
- A member with no permissions, with the IDs of real records of the tenant. The answer must be 403.
- The same member with unknown IDs. The answer must be 403, so that the answer does not tell which IDs exist.

An entrypoint that names a record gets three more calls. It names a record when it has a route value `{id}` or `{identifier}`, a tool argument `id` or `identifier`, or a gRPC request field `id` or `identifier`:

- An admin with all permissions, with the IDs of records of another tenant. The answer must be 404.
- The same admin with unknown IDs. The answer must be 404.
- The control call, last: an admin with all permissions, with the IDs of records of the own tenant. The answer must be a success, or 409 for the stale version of the sample body. A 404 here shows that the entrypoint reads an ID of the wrong kind, so the two 404 calls prove nothing. The control call uses a new tenant of its own, so that a delete does not remove a record that the other calls need.

The sweep does not check a reference to a record in a request body, a query value, or another tool argument or gRPC field, for example `tenantId`. For such a reference, write a cross-tenant test in the tests of the feature.

Thus do not write the 403 test or the cross-tenant test of a route value `{id}` for each new endpoint. Do these steps:

1. Run the functional tests. If the sweep fails, its message names each endpoint, the caller, the expected status, and the actual status.
2. If the handler has no permission check, add the check. See "Permission checks" in `docs/backend/features-and-cqrs.md`.
3. If each member can use the endpoint with no permission, add it to `OpenToEachMember` in the sweep with the reason. The member call of an entry with the IDs of the tenant must then get a success. The member call with unknown IDs must get a success or 404. If the endpoint must also take a call with no user, give its entry `Anonymous: true`. The call with no user sends unknown IDs, so it must then get a success or 404. Each other endpoint must give 401 to a call with no user. The Server has no fallback policy, so an endpoint with no `.RequireAuthorization()` takes anonymous calls, and the sweep shows it as a gap.
4. If the sweep cannot build the request, its message tells what to add. A route value `{id}` under a new first segment needs a record of that kind in `AddTenantAsync`.
5. Map each HTTP endpoint as an `IEndpoint` in the versioned group. A route outside the group is a gap, unless it is in `InfrastructureRoutes` of the sweep with the reason.

The sweep checks the permission of each endpoint, not of each DTO field. For a field with data of another permission, write the test of rule 12 of DTOs in `docs/backend/dtos-and-mappings.md`.

## When to run each Playwright suite

`pnpm test:e2e` (functional):

- Run it after each change in `frontend/src`. It needs no stack. It runs each test in three zones, and it takes two to three minutes.
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

- Each functional test runs in three zones: `UTC`, `Pacific/Kiritimati` (UTC+14), and `Pacific/Pago_Pago` (UTC-11). A test of a page with no place sets its clock with `instantIn(wallClock, browserZone())` from `@tests/support/clock`.
- A time of a place shows and reads in the zone of the place (`docs/frontend/code-style.md`, section "Dates and times"). Test it with a place in a zone far from the browser, at an instant at which the date of the place is not the browser date or the UTC date. See "Time zones" in `docs/frontend/testing.md`. The test `a date filter of a place in another zone` in `filter-demo.spec.ts` checks this rule for the date filter, and `date-filter.test.tsx` and `calendar.test.tsx` check it for the controls.
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
- The unit tests run in `Pacific/Pago_Pago` and the component tests in `Pacific/Kiritimati`. See "Time zones" in `docs/frontend/testing.md`.
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

For a .NET test, mark the parts with `// Arrange`, `// Act`, and `// Assert` (`docs/backend.md`, section "Tests"). `TestMarkerTests` fails on each test without them.

If the change adds a command that runs again, takes a lock, or checks a rule over other rows, add a race test.

If a test proves a rule of a spec in `docs/specs/`, name the rule ID on the test: `[Property("Rule", "R-1")]` on a .NET test, or the tag `@R-1` on a frontend test. When a slice builds a rule, remove `(later)` from its row. See `docs/specs/TEMPLATE.md`, section "Rules".
