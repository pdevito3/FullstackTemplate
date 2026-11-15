import type { Page } from '@playwright/test'
import { QUERY_RETRIES } from '@/lib/query-client'

/** The last answer of a GET that fails each try, after which the page shows the error. */
export function failedLoad(page: Page, path: string) {
  let answers = 0
  return page.waitForResponse(
    (r) => r.request().method() === 'GET' && new URL(r.url()).pathname === path && ++answers === QUERY_RETRIES + 1,
  )
}

/** Two frames, so that the page has rendered each answer that it got. */
export function nextFrames(page: Page) {
  return page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))))
}
