import { test } from '@playwright/test'
import { parseDateTime, toZoned } from '@internationalized/date'

export function browserZone(): string {
  const zone = test.info().project.use.timezoneId
  if (!zone) throw new Error(`The project ${test.info().project.name} sets no timezoneId.`)
  return zone
}

export function instantIn(wallClock: string, zone: string): Date {
  return toZoned(parseDateTime(wallClock), zone).toDate()
}
