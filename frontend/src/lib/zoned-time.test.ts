import { CalendarDate, parseDate, parseDateTime } from '@internationalized/date'
import { describe, expect, it } from 'vitest'

import {
  browserTimeZone,
  formatDate,
  formatInstant,
  instantAt,
  isRepeatedAt,
  offsetAt,
  parseTimeZone,
  startOfDayAt,
  springGapMessage,
  timeZoneLabel,
  todayAt,
  wallClockAt,
} from '@/lib/zoned-time'

const chicago = parseTimeZone('America/Chicago')
const kiritimati = parseTimeZone('Pacific/Kiritimati')
const pagoPago = parseTimeZone('Pacific/Pago_Pago')
// 10:30 UTC on August 15 is 23:30 on August 14 in Pago Pago and 00:30 on August 16 in Kiritimati.
const threeDatesAtOnce = new Date('2026-08-15T10:30:00Z')
// On November 1, 2026, Chicago shows 01:00 to 02:00 twice: first at UTC-5, then at UTC-6.
const firstOneThirty = new Date('2026-11-01T06:30:00Z')
const secondOneThirty = new Date('2026-11-01T07:30:00Z')

describe('the zone of the unit tests', () => {
  it('is Pacific/Pago_Pago, so that a test that takes a date from UTC or from the machine fails', () => {
    expect(browserTimeZone()).toBe('Pacific/Pago_Pago')
  })
})

describe('parseTimeZone', () => {
  it('refuses a zone that the browser does not know', () => {
    expect(() => parseTimeZone('Mars/Olympus_Mons')).toThrow(RangeError)
  })

  it('gives the canonical ID of a zone typed in another case', () => {
    expect(parseTimeZone('america/chicago')).toBe('America/Chicago')
  })

  it('keeps an ID that the browser knows by an older name, as the Server holds it', () => {
    expect(parseTimeZone('Asia/Kolkata')).toBe('Asia/Kolkata')
    expect(parseTimeZone('Europe/Kyiv')).toBe('Europe/Kyiv')
  })
})

describe('todayAt', () => {
  it('takes today from the zone of the place, not from UTC or the browser', () => {
    expect(todayAt(kiritimati, threeDatesAtOnce)).toEqual(new CalendarDate(2026, 8, 16))
    expect(todayAt(pagoPago, threeDatesAtOnce)).toEqual(new CalendarDate(2026, 8, 14))
  })
})

describe('wallClockAt', () => {
  it('gives the date and the time at the place', () => {
    expect(wallClockAt(threeDatesAtOnce, kiritimati).toString()).toBe('2026-08-16T00:30:00')
  })
})

describe('instantAt', () => {
  it('reads a wall clock time in the zone of the place', () => {
    expect(instantAt(parseDateTime('2026-08-15T09:00'), pagoPago)).toEqual(new Date('2026-08-15T20:00:00Z'))
  })

  it('gives null for a time in the spring gap', () => {
    expect(instantAt(parseDateTime('2026-03-08T02:30'), chicago)).toBeNull()
  })

  it('gives the first instant of a time in the repeated fall hour', () => {
    expect(instantAt(parseDateTime('2026-11-01T01:30'), chicago)).toEqual(firstOneThirty)
  })
})

describe('startOfDayAt', () => {
  it('is local midnight', () => {
    expect(startOfDayAt(parseDate('2026-08-15'), kiritimati)).toEqual(new Date('2026-08-14T10:00:00Z'))
  })

  it('is the end of the gap on a day whose midnight is in a gap', () => {
    // Asuncion moved from 00:00 to 01:00 on October 6, 2024, from UTC-4 to UTC-3.
    expect(startOfDayAt(parseDate('2024-10-06'), parseTimeZone('America/Asuncion'))).toEqual(new Date('2024-10-06T04:00:00Z'))
  })
})

describe('offsetAt', () => {
  it('writes the offset of the place as ISO 8601 does', () => {
    expect(offsetAt(threeDatesAtOnce, kiritimati)).toBe('+14:00')
    expect(offsetAt(threeDatesAtOnce, pagoPago)).toBe('-11:00')
    expect(offsetAt(threeDatesAtOnce, parseTimeZone('Asia/Kolkata'))).toBe('+05:30')
  })
})

describe('isRepeatedAt', () => {
  it('is true for both instants of a time in the repeated fall hour, and false for the hour before', () => {
    expect(isRepeatedAt(firstOneThirty, chicago)).toBe(true)
    expect(isRepeatedAt(secondOneThirty, chicago)).toBe(true)
    expect(isRepeatedAt(new Date('2026-11-01T05:30:00Z'), chicago)).toBe(false)
  })
})

describe('formatDate', () => {
  it('shows a date with no time on its own day in a zone west of UTC', () => {
    expect(formatDate(parseDate('2026-03-14'), { weekday: 'short', month: 'short', day: 'numeric' }, 'en-US')).toBe('Sat, Mar 14')
  })
})

describe('formatInstant', () => {
  const time = { hour: 'numeric', minute: '2-digit' } as const

  it('shows the time at the place', () => {
    expect(formatInstant(new Date('2026-08-15T20:00:00Z'), pagoPago, time, 'en-US')).toBe('9:00 AM')
  })

  it('names the zone when asked', () => {
    expect(formatInstant(new Date('2026-08-15T15:00:00Z'), chicago, { ...time, showZone: true }, 'en-US')).toBe('10:00 AM CDT')
  })

  it('shows the offset of a time in the repeated fall hour, so that its two instants differ', () => {
    expect(formatInstant(firstOneThirty, chicago, time, 'en-US')).toBe('1:30 AM GMT-5')
    expect(formatInstant(secondOneThirty, chicago, time, 'en-US')).toBe('1:30 AM GMT-6')
  })

  it('shows no offset for a date with no time in the repeated hour', () => {
    expect(formatInstant(firstOneThirty, chicago, { month: 'short', day: 'numeric' }, 'en-US')).toBe('Nov 1')
  })
})

describe('timeZoneLabel', () => {
  it.each([
    ['Pacific/Pago_Pago', 'American Samoa Time'],
    ['Europe/Berlin', 'Germany Time'],
    ['Asia/Bangkok', 'Thailand Time'],
    ['America/New_York', 'Eastern Time'],
    ['America/Phoenix', 'Mountain Standard Time'],
    ['UTC', 'Coordinated Universal Time'],
    ['Etc/GMT+5', 'GMT-05:00'],
  ])('names %s as a user knows it: %s', (zone, name) => {
    expect(timeZoneLabel(parseTimeZone(zone))).toBe(name)
  })
})

describe('springGapMessage', () => {
  it('names the zone of the place as timeZoneLabel does', () => {
    expect(springGapMessage(parseTimeZone('America/Chicago'))).toBe(
      'This time does not exist in Central Time on that day, because the clocks go forward. Choose another time.',
    )
  })

  it('names no zone for a time in the zone of the browser', () => {
    expect(springGapMessage()).toBe('This time does not exist on that day, because the clocks go forward. Choose another time.')
  })
})
