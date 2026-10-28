import { describe, expect, it } from 'vitest'

import { parseTimeZone } from '@/lib/zoned-time'

import type { DateType, DateValue } from '../types'
import { toQueryKitString } from './querykit-converter'

// 20:00 UTC on August 15 is 10:00 on August 16 in Kiritimati, and 09:00 on August 15 in Pago Pago, the zone of the
// unit tests.
const startsAt = new Date('2026-08-15T20:00:00Z')

function queryFor(dateType: DateType, timeZone?: DateValue['timeZone']): string {
  return toQueryKitString({
    rootLogicalOperator: 'AND',
    filters: [
      {
        id: '1',
        propertyKey: 'startsAt',
        propertyLabel: 'Starts At',
        controlType: 'date',
        operator: '==',
        value: { mode: 'on', startDate: startsAt, dateType, timeZone },
      },
    ],
  })
}

describe('a date filter of a place', () => {
  const kiritimati = parseTimeZone('Pacific/Kiritimati')

  it.each([
    ['date', 'startsAt == "2026-08-16"'],
    ['datetime', 'startsAt == "2026-08-16T10:00:00"'],
    ['datetimeUtc', 'startsAt == "2026-08-15T20:00:00Z"'],
    ['datetimeOffset', 'startsAt == "2026-08-16T10:00:00+14:00"'],
  ] as const)('writes a %s in the zone of the place', (dateType, query) => {
    expect(queryFor(dateType, kiritimati)).toBe(query)
  })
})

describe('a date filter with no zone', () => {
  it('writes the wall clock time of the browser', () => {
    expect(queryFor('datetimeOffset')).toBe('startsAt == "2026-08-15T09:00:00-11:00"')
  })
})
