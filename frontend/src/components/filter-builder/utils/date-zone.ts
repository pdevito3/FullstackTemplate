import { browserTimeZone, parseTimeZone, type TimeZone } from '@/lib/zoned-time'

import type { DateType } from '../types'

const utc = parseTimeZone('UTC')

/**
 * The zone that a date value keeps its wall clock time in. A `datetime` with no zone of a place is a wall clock time with
 * no zone, so it is kept in UTC, which skips no time. The other types with no zone hold a time of the user, in the zone
 * of the browser.
 */
export function wallClockZone(dateType: DateType | undefined, placeZone: TimeZone | undefined): TimeZone {
  if (placeZone) return placeZone
  return dateType === 'datetime' ? utc : browserTimeZone()
}
