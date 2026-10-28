// Setup for the component tests in browser mode (*.test.tsx).
// The app styles load, so that a test sees the real layout and the real visibility of each element.
import '@/index.css'

// The commands are in vitest.commands.ts. They run in Node and drive the Playwright page.
declare module 'vitest/browser' {
  interface BrowserCommands {
    /** Holds each request for `path` (pathname and query) until `releaseRequests(path)`. */
    holdRequests: (path: string) => Promise<void>
    /** Lets the held requests for `path` go on. */
    releaseRequests: (path: string) => Promise<void>
    /** Moves the mouse onto the element with `role` and `name` without a scroll of the page. */
    movePointerOnto: (role: string, name: string) => Promise<void>
  }
}
