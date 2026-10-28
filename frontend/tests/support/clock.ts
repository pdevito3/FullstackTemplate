import { test } from '@playwright/test'
import { parseDateTime, toZoned } from '@internationalized/date'

/** The zone of the browser in this run. Each project of playwright.config.ts sets one. */
export function browserZone(): string {
  const zone = test.info().project.use.timezoneId
  if (!zone) throw new Error(`The project ${test.info().project.name} sets no timezoneId.`)
  return zone
}

/** The instant of a wall clock time in a zone, for example `instantIn('2026-08-15T12:00', 'Pacific/Pago_Pago')`. */
export function instantIn(wallClock: string, zone: string): Date {
  return toZoned(parseDateTime(wallClock), zone).toDate()
}
