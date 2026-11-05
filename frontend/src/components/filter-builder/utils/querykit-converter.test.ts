import { CalendarDateTime } from '@internationalized/date'
import { describe, expect, expectTypeOf, it } from 'vitest'

import { parseTimeZone } from '@/lib/zoned-time'

import type { DateValue } from '../types'
import { toQueryKitString } from './querykit-converter'

// 20:00 UTC on August 15 is 10:00 on August 16 in Kiritimati, and 09:00 on August 15 in Pago Pago, the zone of the
// unit tests.
const startsAt = new Date('2026-08-15T20:00:00Z')

function queryFor(value: DateValue): string {
  return toQueryKitString({
    rootLogicalOperator: 'AND',
    filters: [
      {
        id: '1',
        propertyKey: 'startsAt',
        propertyLabel: 'Starts At',
        controlType: 'date',
        operator: '==',
        value,
      },
    ],
  })
}

describe('a date filter of a place', () => {
  const kiritimati = parseTimeZone('Pacific/Kiritimati')

  it.each([
    ['date', 'startsAt == "2026-08-16"'],
    ['datetimeUtc', 'startsAt == "2026-08-15T20:00:00Z"'],
    ['datetimeOffset', 'startsAt == "2026-08-16T10:00:00+14:00"'],
  ] as const)('writes a %s in the zone of the place', (dateType, query) => {
    expect(queryFor({ mode: 'on', startDate: startsAt, dateType, timeZone: kiritimati })).toBe(query)
  })
})

describe('a date filter with no zone', () => {
  it('writes the wall clock time of the browser', () => {
    expect(queryFor({ mode: 'on', startDate: startsAt, dateType: 'datetimeOffset' })).toBe('startsAt == "2026-08-15T09:00:00-11:00"')
  })

  it('writes a datetime as its wall clock time, because a datetime has no zone', () => {
    const value: DateValue = { mode: 'on', startDate: new CalendarDateTime(2026, 8, 15, 20), dateType: 'datetime' }

    expect(queryFor(value)).toBe('startsAt == "2026-08-15T20:00:00"')
  })
})

function textQuery(value: string): string {
  return toQueryKitString({
    rootLogicalOperator: 'AND',
    filters: [
      {
        id: '1',
        propertyKey: 'title',
        propertyLabel: 'Title',
        controlType: 'text',
        operator: '@=*',
        value,
      },
    ],
  })
}

describe('a text filter', () => {
  it.each([
    ['a word', 'Paul', 'title @=* "Paul"'],
    ['a space', 'a b', 'title @=* "a b"'],
    ['a hyphen', 'a-b', 'title @=* "a-b"'],
    ['a dot, which QueryKit reads as a property path when it has no quotes', 'a.b', 'title @=* "a.b"'],
    ['the word null, which QueryKit reads as no value when it has no quotes', 'null', 'title @=* "null"'],
    ['a number', '42', 'title @=* "42"'],
    ['a backslash, which QueryKit keeps as it is', 'a\\b', 'title @=* "a\\b"'],
    ['an apostrophe', "it's", 'title @=* "it\'s"'],
    ['operators and brackets', 'a && b || (c) [d]', 'title @=* "a && b || (c) [d]"'],
    ['a quote, in a padded raw literal', 'a"b', 'title @=* """ a"b """'],
    ['three quotes, in a padded raw literal with longer delimiters', 'a"""b', 'title @=* """" a"""b """"'],
    ['a quote at each end, in a padded raw literal', '"Paul"', 'title @=* """ "Paul" """'],
    ['a quote at the end, in a padded raw literal', 'quote"', 'title @=* """ quote" """'],
    ['only quotes, in a padded raw literal', '""', 'title @=* """ "" """'],
  ])('with %s', (_, value, query) => {
    expect(textQuery(value)).toBe(query)
  })

  it.each(['[DRAFT]', '[]', '[DRAFT] plan', '[', ']', 'a]b[c'])('with %s, which QueryKit does not read as a list', (value) => {
    expect(textQuery(value)).toBe(`title @=* "${value}"`)
  })
})

describe('a multiselect filter', () => {
  it('quotes each value, so that a value with a hyphen or a quote is read as text', () => {
    const query = toQueryKitString({
      rootLogicalOperator: 'AND',
      filters: [
        {
          id: '1',
          propertyKey: 'status',
          propertyLabel: 'Status',
          controlType: 'multiselect',
          operator: '^^',
          value: ['open', 'in-progress', 'a"b', '"Paul"', '[DRAFT]'],
        },
      ],
    })

    expect(query).toBe('status ^^ ["open", "in-progress", """ a"b """, """ "Paul" """, "[DRAFT]"]')
  })
})

// These are type tests. The Vitest typecheck in `pnpm test` checks them.
describe('the type of a date value', () => {
  it('holds a datetime as a wall clock time and each other type as an instant', () => {
    expectTypeOf<{ mode: 'on'; startDate: CalendarDateTime; dateType: 'datetime' }>().toExtend<DateValue>()
    expectTypeOf<{ mode: 'on'; startDate: Date; dateType: 'datetimeUtc' }>().toExtend<DateValue>()
    expectTypeOf<{ mode: 'on'; startDate: Date }>().toExtend<DateValue>()
  })

  it('refuses a datetime that holds an instant, because an instant moves with the zone of the reader', () => {
    expectTypeOf<{ mode: 'on'; startDate: Date; dateType: 'datetime' }>().not.toExtend<DateValue>()
  })

  it('refuses an instant that holds a wall clock time, because a wall clock time has no zone', () => {
    expectTypeOf<{ mode: 'on'; startDate: CalendarDateTime; dateType: 'datetimeUtc' }>().not.toExtend<DateValue>()
  })
})
