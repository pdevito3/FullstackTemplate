import { expect, type Page } from '@playwright/test'
import { QUERY_RETRIES } from '@/lib/query-client'
import { bffPaths } from './api/bff-api'

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

/**
 * Opens a URL that the SPA sends to the login as it loads, and waits until the browser is on the login. The app can
 * take more than the 5 s of an expect to load on a slow machine, so the wait is for the answer of the login, which the
 * test timeout limits.
 */
export async function openAndWaitForLogin(page: Page, url: string) {
  const login = page.waitForResponse((r) => r.request().method() === 'GET' && new URL(r.url()).pathname === bffPaths.login)
  await page.goto(url)
  await login
  await expect.poll(() => new URL(page.url()).pathname).toBe(bffPaths.login)
}
