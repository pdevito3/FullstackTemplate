import type { BrowserCommand } from 'vitest/node'

// Browser commands for the component tests. They run in Node and drive the Playwright page.
// A test calls them with `commands` from 'vitest/browser'.

type Page = Parameters<BrowserCommand<[]>>[0]['page']
type RouteHandler = Parameters<Page['route']>[1]

interface Hold {
  release: () => void
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
  const matcher = matches(path)
  const handler: RouteHandler = async (route) => {
    await held
    await route.continue()
  }
  holds.set(path, { release, matcher, handler })
  await page.route(matcher, handler)
}

/** Lets the held requests for `path` go on, and removes the route. Does nothing if `path` is not held. */
export const releaseRequests: BrowserCommand<[path: string]> = async ({ page }, path) => {
  const hold = holds.get(path)
  if (!hold) return
  holds.delete(path)
  hold.release()
  await page.unroute(hold.matcher, hold.handler)
}
