import { CalendarDateTime } from '@internationalized/date'
import { describe, expect, it } from 'vitest'

import { parseTimeZone } from '@/lib/zoned-time'

import type { DateValue, Filter } from '../types'
import { formatFilterValue, getFilterName } from './filter-name'

function filterOf(value: DateValue): Filter {
  return { id: '1', propertyKey: 'startsAt', propertyLabel: 'Starts At', controlType: 'date', operator: '==', value }
}

describe('the value of a date filter', () => {
  it('shows the date and time of a datetime as it is, because a datetime has no zone', () => {
    // As an instant in UTC, 00:30 on August 15 is August 14 in Pago Pago, the zone of the unit tests.
    const value: DateValue = { mode: 'on', startDate: new CalendarDateTime(2026, 8, 15, 0, 30), dateType: 'datetime' }

    expect(formatFilterValue(filterOf(value))).toBe('Aug 15, 2026, 12:30 AM')
  })

  it('shows an instant in the zone of the browser', () => {
    // 05:00 UTC on August 15 is 18:00 on August 14 in Pago Pago.
    const value: DateValue = { mode: 'on', startDate: new Date('2026-08-15T05:00:00Z'), dateType: 'datetimeUtc' }

    expect(formatFilterValue(filterOf(value))).toBe('Aug 14, 2026, 6:00 PM')
  })

  it('shows an instant of a place in the zone of the place, and names the zone', () => {
    // 05:00 UTC on August 15 is 01:00 on August 15 in New York.
    const value: DateValue = {
      mode: 'on',
      startDate: new Date('2026-08-15T05:00:00Z'),
      dateType: 'datetimeUtc',
      timeZone: parseTimeZone('America/New_York'),
    }

    expect(formatFilterValue(filterOf(value))).toBe('Aug 15, 2026, 1:00 AM EDT')
  })

  it('shows no time for a date', () => {
    const value: DateValue = { mode: 'on', startDate: new Date(2026, 7, 15), dateType: 'date' }

    expect(formatFilterValue(filterOf(value))).toBe('Aug 15, 2026')
  })
})

describe('the name of a date filter', () => {
  const start = new Date(2026, 10, 3)
  const end = new Date(2026, 10, 20)

  it.each<[string, DateValue]>([
    ['Starts At | On | Nov 3, 2026', { mode: 'on', startDate: start }],
    ['Starts At | Not on | Nov 3, 2026', { mode: 'excluding', startDate: start }],
    ['Starts At | Before | Nov 3, 2026', { mode: 'before', startDate: start }],
    ['Starts At | After | Nov 3, 2026', { mode: 'after', startDate: start }],
    ['Starts At | Between | Nov 3, 2026 - Nov 20, 2026', { mode: 'between', startDate: start, endDate: end }],
    ['Starts At | Not between | Nov 3, 2026 - Nov 20, 2026', { mode: 'between', startDate: start, endDate: end, exclude: true }],
    ['Starts At | On or after | Nov 3, 2026', { mode: 'between', startDate: start }],
  ])('reads %s, as the QueryKit string does', (name, value) => {
    expect(getFilterName(filterOf(value))).toBe(name)
  })
})
