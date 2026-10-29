import { describe, expect, it } from 'vitest'

import { parseTimeZone } from '@/lib/zoned-time'

import { whereIs } from './workshop-fields-schema'

describe('whereIs', () => {
  it.each([
    ['America/Chicago', 'in Chicago'],
    ['Pacific/Pago_Pago', 'in Pago Pago'],
    ['America/Argentina/Buenos_Aires', 'in Buenos Aires'],
    ['UTC', 'at the venue'],
    ['Etc/GMT+5', 'at the venue'],
    ['Singapore', 'at the venue'],
  ])('names where %s is as "%s", with no time zone ID', (zone, where) => {
    expect(whereIs(parseTimeZone(zone))).toBe(where)
  })
})
