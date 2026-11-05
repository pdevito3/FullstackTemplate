// Setup for the component tests in browser mode (*.test.tsx).
// The app styles load, so that a test sees the real layout and the real visibility of each element.
import '@/index.css'

import { afterEach, beforeEach } from 'vitest'
import { commands } from 'vitest/browser'
import { cleanup } from 'vitest-browser-react'

import { testIdHeader, type Answer } from './answer'

// The commands are in vitest.commands.ts. They run in Node and drive the Playwright page.
declare module 'vitest/browser' {
  interface BrowserCommands {
    resetRequestsFor: (testId: string) => Promise<void>
    /** Answers each request that matches an answer. A later answer wins. `[]` removes all answers. */
    answerRequests: (answers: Answer[]) => Promise<void>
    /** Returns each request of this test to `/api/*` or `/bff/*` that no answer matched since the last call, and forgets them. */
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

let testId = ''

const send = XMLHttpRequest.prototype.send
XMLHttpRequest.prototype.send = function (body) {
  this.setRequestHeader(testIdHeader, testId)
  send.call(this, body)
}

// A request of the last test can reach the commands after this hook, also when the unmount aborted it.
beforeEach(async ({ task }) => {
  await cleanup()
  testId = task.id
  await commands.resetRequestsFor(task.id)
})

// A request with no answer gets an error from the dev server. A test can still pass by mistake, for example when
// the page shows the same text for an error and for an empty answer.
afterEach(async () => {
  const unanswered = await commands.takeUnansweredRequests()
  if (unanswered.length > 0) {
    throw new Error(`No answer for these requests: ${unanswered.join(', ')}. Answer them with commands.answerRequests.`)
  }
})
