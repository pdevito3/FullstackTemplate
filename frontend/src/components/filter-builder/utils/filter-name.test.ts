import { CalendarDateTime } from '@internationalized/date'
import { describe, expect, it } from 'vitest'

import type { DateValue, Filter } from '../types'
import { formatFilterValue } from './filter-name'

function filterOf(value: DateValue): Filter {
  return { id: '1', propertyKey: 'startsAt', propertyLabel: 'Starts At', controlType: 'date', operator: '==', value }
}

describe('the value of a date filter', () => {
  it('shows the date of a datetime as it is, because a datetime has no zone', () => {
    // As an instant in UTC, 00:30 on August 15 is August 14 in Pago Pago, the zone of the unit tests.
    const value: DateValue = { mode: 'on', startDate: new CalendarDateTime(2026, 8, 15, 0, 30), dateType: 'datetime' }

    expect(formatFilterValue(filterOf(value))).toBe('Aug 15, 2026')
  })

  it('shows an instant in the zone of the browser', () => {
    // 05:00 UTC on August 15 is 18:00 on August 14 in Pago Pago.
    const value: DateValue = { mode: 'on', startDate: new Date('2026-08-15T05:00:00Z'), dateType: 'datetimeUtc' }

    expect(formatFilterValue(filterOf(value))).toBe('Aug 14, 2026')
  })
})
