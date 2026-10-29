// Setup for the component tests in browser mode (*.test.tsx).
// The app styles load, so that a test sees the real layout and the real visibility of each element.
import '@/index.css'

import { afterEach, beforeEach } from 'vitest'
import { commands } from 'vitest/browser'
import { cleanup } from 'vitest-browser-react'

import type { Answer } from './answer'

// The commands are in vitest.commands.ts. They run in Node and drive the Playwright page.
declare module 'vitest/browser' {
  interface BrowserCommands {
    /** Answers each request that matches an answer. A later answer wins. `[]` removes all answers. */
    answerRequests: (answers: Answer[]) => Promise<void>
    /** Returns each request to `/api/*` or `/bff/*` that no answer matched since the last call, and forgets them. */
    takeUnansweredRequests: () => Promise<string[]>
    /** Holds each request for `path` (pathname and query) until `releaseRequests(path)`. */
    holdRequests: (path: string) => Promise<void>
    /** Lets the held requests for `path` go on. */
    releaseRequests: (path: string) => Promise<void>
    /** Moves the mouse onto the element with `role` and `name` without a scroll of the page. */
    movePointerOnto: (role: string, name: string) => Promise<void>
    /** Presses the left mouse button where the mouse is. */
    pressPointer: () => Promise<void>
    /** Releases the left mouse button where the mouse is. */
    releasePointer: () => Promise<void>
  }
}

// vitest-browser-react unmounts the page of the last test only before the next test. Unmount it first, so that a
// late request of the last test does not count as one of this test.
beforeEach(async () => {
  await cleanup()
  await commands.answerRequests([])
  await commands.takeUnansweredRequests()
})

// A request with no answer gets an error from the dev server. A test can still pass by mistake, for example when
// the page shows the same text for an error and for an empty answer.
afterEach(async () => {
  const unanswered = await commands.takeUnansweredRequests()
  if (unanswered.length > 0) {
    throw new Error(`No answer for these requests: ${unanswered.join(', ')}. Answer them with commands.answerRequests.`)
  }
})
