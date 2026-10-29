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

  it('writes a datetime as the wall clock time that it keeps in UTC, because a datetime with no zone has no zone', () => {
    expect(queryFor('datetime')).toBe('startsAt == "2026-08-15T20:00:00"')
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
    ['a quote, in a raw literal', 'a"b', 'title @=* """a"b"""'],
    ['three quotes, in a raw literal with longer delimiters', 'a"""b', 'title @=* """"a"""b""""'],
  ])('with %s', (_, value, query) => {
    expect(textQuery(value)).toBe(query)
  })

  it.each(['"Paul"', '"Quoted', 'quote"', '"', '""', '[DRAFT]', '[]'])(
    'with %s leaves the filter out, because QueryKit cannot hold the value and would match other text',
    (value) => {
      expect(textQuery(value)).toBe('')
    },
  )

  it.each(['[DRAFT] plan', '[', ']', 'a]b[c'])('with %s, which QueryKit does not read as a list', (value) => {
    expect(textQuery(value)).toBe(`title @=* "${value}"`)
  })
})

describe('a filter that QueryKit cannot hold', () => {
  const firstName = (id: string, value: string) =>
    ({ id, propertyKey: 'firstName', propertyLabel: 'First Name', controlType: 'text', operator: '==', value }) as const

  it('is left out of the root, and the other filters are sent', () => {
    const query = toQueryKitString({
      rootLogicalOperator: 'OR',
      filters: [firstName('1', 'Jane'), firstName('2', '"John"'), firstName('3', 'Ann')],
    })

    expect(query).toBe('firstName == "Jane" || firstName == "Ann"')
  })

  it('is left out of its group, and a group with no other filter is left out', () => {
    const query = toQueryKitString({
      rootLogicalOperator: 'AND',
      filters: [
        { id: 'g1', type: 'group', logicalOperator: 'OR', filters: [firstName('1', 'Jane'), firstName('2', '[DRAFT]')] },
        { id: 'g2', type: 'group', logicalOperator: 'OR', filters: [firstName('3', '"John"')] },
        firstName('4', 'Ann'),
      ],
    })

    expect(query).toBe('(firstName == "Jane") && firstName == "Ann"')
  })

  it('in a multiselect, is left out with the whole filter', () => {
    const query = toQueryKitString({
      rootLogicalOperator: 'AND',
      filters: [
        { id: '1', propertyKey: 'status', propertyLabel: 'Status', controlType: 'multiselect', operator: '^^', value: ['open', '"Paul"'] },
      ],
    })

    expect(query).toBe('')
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
          value: ['open', 'in-progress', 'a"b', '[DRAFT]'],
        },
      ],
    })

    expect(query).toBe('status ^^ ["open", "in-progress", """a"b""", "[DRAFT]"]')
  })
})
