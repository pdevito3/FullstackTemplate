import { test, type Page } from '@playwright/test'
import { parseDateTime, toZoned } from '@internationalized/date'

export function browserZone(): string {
  const zone = test.info().project.use.timezoneId
  if (!zone) throw new Error(`The project ${test.info().project.name} sets no timezoneId.`)
  return zone
}

export function instantIn(wallClock: string, zone: string): Date {
  return toZoned(parseDateTime(wallClock), zone).toDate()
}

/**
 * Cuts the time limit that the app gives each request to `path` down to `limitMs`. It applies to the pages that open
 * after the call. The browser, not a timer of the page, counts the `timeout` of an XMLHttpRequest, so `page.clock` does
 * not reach it.
 */
export async function shortenRequestTimeLimit(page: Page, path: string, limitMs: number) {
  await page.addInitScript(
    ({ path, limitMs }) => {
      const pathOf = new WeakMap<XMLHttpRequest, string>()
      const open = XMLHttpRequest.prototype.open
      XMLHttpRequest.prototype.open = function (this: XMLHttpRequest, ...args: Parameters<typeof open>) {
        pathOf.set(this, new URL(args[1], location.href).pathname)
        open.apply(this, args)
      } as typeof open
      const timeout = Object.getOwnPropertyDescriptor(XMLHttpRequest.prototype, 'timeout')!
      Object.defineProperty(XMLHttpRequest.prototype, 'timeout', {
        configurable: true,
        get: timeout.get,
        set(this: XMLHttpRequest, ms: number) {
          timeout.set!.call(this, ms && pathOf.get(this) === path ? Math.min(ms, limitMs) : ms)
        },
      })
    },
    { path, limitMs },
  )
}
