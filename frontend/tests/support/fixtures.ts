import { test as base, expect, type Request } from '@playwright/test'
import { bffApi } from './api/bff-api'
import { isMockedPath, requestRecorder, type HttpMethod, type MockRoute, type ServedRequest } from './api/mock-route'
import { as, type Identity } from './auth/actors'

export interface Api {
  /** Serve these canned responses for the rest of the test. A later route wins over an earlier one. */
  mock(...routes: MockRoute[]): void
  /** The last request that the seam served for this method and path, or undefined if there was none. */
  last(method: HttpMethod, path: string): ServedRequest | undefined
  /** Each request that the seam served for this method and path, in order. */
  all(method: HttpMethod, path: string): ServedRequest[]
}

// `postDataJSON` throws on a body that is not JSON. That leaves the route unfulfilled and the test
// times out, so a recorder must not let it throw.
function postBody(request: Request): unknown {
  try {
    return request.postDataJSON()
  } catch {
    return undefined
  }
}

export const test = base.extend<{ actor: Identity; api: Api }>({
  actor: [as.anonymous(), { option: true }],

  // `provide`, not `use`: a bare `use(...)` reads as a React hook to the lint rule.
  api: [
    async ({ page, actor, baseURL }, provide) => {
      const origin = new URL(baseURL!).origin
      // The actor is the first mock, so a test can still answer /bff/user in another way.
      const routes: MockRoute[] = [bffApi.userIs(actor)]
      const served = requestRecorder()
      const missed: string[] = []

      await page.route(
        (url) => url.origin === origin && isMockedPath(url.pathname),
        async (route, request) => {
          const url = new URL(request.url())
          const method = request.method().toLowerCase()
          const match = routes.findLast((r) => r.method === method && r.path === url.pathname)

          // Playwright fails a test on a failed assertion, never on an HTTP status. So the 501 alone
          // proves nothing, and the miss is asserted at teardown.
          if (!match) {
            missed.push(`${method.toUpperCase()} ${url.pathname}`)
            await route.fulfill({ status: 501, body: `No mock for ${method} ${url.pathname}` })
            return
          }

          served.add(match.method, url, new Headers(await request.allHeaders()), postBody(request))
          if (match.delayMs) await new Promise((resolve) => setTimeout(resolve, match.delayMs))
          await route.fulfill({
            status: match.status,
            headers: match.headers,
            ...(match.body === undefined
              ? {}
              : { contentType: 'application/json', body: JSON.stringify(match.body) }),
          })
        },
      )

      await provide({
        mock: (...added) => routes.push(...added),
        last: served.last,
        all: served.all,
      })

      // Let an in-flight handler finish, so that a late call is still recorded.
      await page.unrouteAll({ behavior: 'wait' })
      expect(missed, 'the page called an API route that no test mocked').toEqual([])
    },
    { auto: true },
  ],
})

export { expect }
