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
    // Not continue(): Playwright's unroute in releaseRequests can continue an in-flight request first, and a second continue throws "Route is already handled!".
    await route.fallback()
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

type Role = Parameters<Page['getByRole']>[0]

/**
 * Moves the mouse onto the element with `role` and `name`, once the element stops moving. `userEvent.hover`
 * scrolls the page first, and a scroll of the page closes an open list.
 */
export const movePointerOnto: BrowserCommand<[role: Role, name: string]> = async ({ iframe, page }, role, name) => {
  const target = iframe.getByRole(role, { name, exact: true })
  await target.waitFor()
  let box = await target.boundingBox()
  for (let tries = 0; tries < 50; tries++) {
    await new Promise((resolve) => setTimeout(resolve, 50))
    const next = await target.boundingBox()
    if (box && next && JSON.stringify(box) === JSON.stringify(next)) {
      await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2)
      return
    }
    box = next
  }
  throw new Error(`The ${role} "${name}" did not stop moving`)
}

/** Presses the left mouse button where the mouse is. */
export const pressPointer: BrowserCommand<[]> = async ({ page }) => {
  await page.mouse.down()
}

/** Releases the left mouse button where the mouse is. */
export const releasePointer: BrowserCommand<[]> = async ({ page }) => {
  await page.mouse.up()
}
