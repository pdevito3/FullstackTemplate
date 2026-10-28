import {
  CalendarDate,
  CalendarDateTime,
  fromAbsolute,
  getLocalTimeZone,
  toCalendarDate,
  toCalendarDateTime,
  toZoned,
} from '@internationalized/date'

/**
 * The IANA time zone of a place, for example `America/Chicago`, as the Server `TimeZoneId` holds it. Make one with
 * `parseTimeZone` from an API field, or take the zone of the browser with `browserTimeZone` on a page with no place.
 */
export type TimeZone = string & { readonly __brand: 'TimeZone' }

/** Reads the zone of a place from an API field. Throws a `RangeError` for a zone that the browser does not know. */
export function parseTimeZone(value: string): TimeZone {
  new Intl.DateTimeFormat('en-US', { timeZone: value })
  return value as TimeZone
}

/** The zone of the browser. Use it only for a time that belongs to the user, not to a place. */
export function browserTimeZone(): TimeZone {
  return getLocalTimeZone() as TimeZone
}

/** The zone as a person reads it, for example `America/Argentina/Buenos Aires`. */
export function timeZoneLabel(zone: TimeZone): string {
  return zone.replaceAll('_', ' ')
}

/** Today at the place, as `ZonedDate.TodayAt` on the Server. The UTC date and the browser date can be another day. */
export function todayAt(zone: TimeZone, now: Date): CalendarDate {
  return toCalendarDate(fromAbsolute(now.getTime(), zone))
}

/** The date and the wall clock time of an instant at the place, as `TimeZoneId.ToLocal` on the Server. */
export function wallClockAt(instant: Date, zone: TimeZone): CalendarDateTime {
  return toCalendarDateTime(fromAbsolute(instant.getTime(), zone))
}

/**
 * The instant of a wall clock time at the place, as `TimeZoneId.ToInstant` on the Server. A time in the spring gap
 * does not exist, so it gives null. A time in the repeated fall hour gives the first of the two instants.
 */
export function instantAt(wallClock: CalendarDateTime, zone: TimeZone): Date | null {
  const zoned = toZoned(wallClock, zone, 'earlier')
  return toCalendarDateTime(zoned).compare(wallClock) === 0 ? zoned.toDate() : null
}

/** The first instant of a date at the place, as `ZonedDate.Start`: local midnight, or the end of a gap at midnight. */
export function startOfDayAt(date: CalendarDate, zone: TimeZone): Date {
  return toZoned(date, zone, 'compatible').toDate()
}

/** The offset of the place from UTC at the instant, as ISO 8601 writes it, for example `-05:00`. */
export function offsetAt(instant: Date, zone: TimeZone): string {
  const minutes = fromAbsolute(instant.getTime(), zone).offset / 60_000
  const sign = minutes < 0 ? '-' : '+'
  const hours = String(Math.trunc(Math.abs(minutes) / 60)).padStart(2, '0')
  return `${sign}${hours}:${String(Math.abs(minutes) % 60).padStart(2, '0')}`
}

/** True when the wall clock of the place shows the time of the instant twice, as on the night that the clocks go back. */
export function isRepeatedAt(instant: Date, zone: TimeZone): boolean {
  const wallClock = wallClockAt(instant, zone)
  return toZoned(wallClock, zone, 'earlier').compare(toZoned(wallClock, zone, 'later')) !== 0
}

/**
 * Formats a date that has no time, such as a `DateOnly` from the Server. It shows the same day in each zone of the
 * browser. `new Date('2026-03-14')` is midnight UTC, which is March 13 in the browser west of UTC.
 */
export function formatDate(date: CalendarDate, options: Intl.DateTimeFormatOptions, locale?: string): string {
  return new Intl.DateTimeFormat(locale, { ...options, timeZone: 'UTC' }).format(date.toDate('UTC'))
}

interface FormatInstantOptions
  extends Omit<Intl.DateTimeFormatOptions, 'timeZone' | 'timeZoneName' | 'dateStyle' | 'timeStyle'> {
  /** Name the zone, for a user who can be in another zone than the place. */
  showZone?: boolean
}

/**
 * Formats an instant in the zone of its place. A time in the repeated fall hour shows its offset, for example
 * `1:30 AM GMT-5`, because the place shows the same wall clock time twice.
 */
export function formatInstant(
  instant: Date,
  zone: TimeZone,
  { showZone = false, ...options }: FormatInstantOptions,
  locale?: string,
): string {
  const isRepeatedTime = options.hour !== undefined && isRepeatedAt(instant, zone)
  const timeZoneName = isRepeatedTime ? 'shortOffset' : showZone ? 'short' : undefined
  return new Intl.DateTimeFormat(locale, { ...options, timeZone: zone, timeZoneName }).format(instant)
}
