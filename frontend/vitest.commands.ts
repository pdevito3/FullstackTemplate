import type { BrowserCommand } from 'vitest/node'

// Browser commands for the component tests. They run in Node and drive the Playwright page.
// A test calls them with `commands` from 'vitest/browser'.

type Page = Parameters<BrowserCommand<[]>>[0]['page']
type RouteHandler = Parameters<Page['route']>[1]

interface Hold {
  release: () => void
  arrived: Promise<void>
  continues: Set<Promise<void>>
  matcher: (url: URL) => boolean
  handler: RouteHandler
}

const holds = new Map<string, Hold>()

function matches(path: string): (url: URL) => boolean {
  return (url) => url.pathname + url.search === path
}

/** Holds each request for `path` (pathname and query) until `releaseRequests(path)`. */
export const holdRequests: BrowserCommand<[path: string]> = async ({ page }, path) => {
  if (holds.has(path)) {
    throw new Error(`The requests for ${path} are already held`)
  }
  let release = () => {}
  const held = new Promise<void>((resolve) => (release = resolve))
  let arrive = () => {}
  const arrived = new Promise<void>((resolve) => (arrive = resolve))
  const matcher = matches(path)
  const continues = new Set<Promise<void>>()
  const handler: RouteHandler = async (route) => {
    arrive()
    await held
    const continued = route.continue()
    continues.add(continued)
    await continued
  }
  holds.set(path, { release, arrived, continues, matcher, handler })
  await page.route(matcher, handler)
  await waitForInterception(page)
}

// The browser intercepts a request only some time after `page.route()` returns. A request in that gap goes to
// the network, so the hold does not hold it, and its late route event throws "Route is already handled".
// Thus the command fetches a probe URL until a route fulfills it. The vitest Playwright provider does the same.
async function waitForInterception(page: Page) {
  const probe = '/__hold_requests_probe__'
  const probeHandler: RouteHandler = (route) => route.fulfill({ status: 204, headers: { 'x-hold-probe': '1' } })
  await page.route(`**${probe}`, probeHandler)
  try {
    const deadline = Date.now() + 5000
    while (Date.now() < deadline) {
      const intercepted = await page
        .evaluate(
          (url) =>
            fetch(url, { cache: 'no-store' }).then(
              (response) => response.headers.has('x-hold-probe'),
              () => false,
            ),
          probe,
        )
        .catch(() => false)
      if (intercepted) return
      await new Promise((resolve) => setTimeout(resolve, 10))
    }
    throw new Error('The request interception did not start within 5 s')
  } finally {
    await page.unroute(`**${probe}`, probeHandler)
  }
}

/**
 * Lets the held requests for `path` go on, and removes the route. Does nothing if `path` is not held.
 *
 * A change of the routes makes Playwright continue each request that a removed handler still holds. The
 * late `route.continue()` of the handler then throws "Route is already handled". Thus the command waits for
 * the first request to reach the handler (5 s at most), and for each continue, before it removes the route.
 * `unroute` has no option for this wait in Playwright 1.64.
 */
export const releaseRequests: BrowserCommand<[path: string]> = async ({ page }, path) => {
  const hold = holds.get(path)
  if (!hold) return
  holds.delete(path)
  await Promise.race([hold.arrived, new Promise((resolve) => setTimeout(resolve, 5000))])
  hold.release()
  // The handler adds its continue after the release, so wait one turn before the read of the set.
  await new Promise((resolve) => setImmediate(resolve))
  await Promise.allSettled(hold.continues)
  await page.unroute(hold.matcher, hold.handler)
}
