import { describe, expect, it } from 'vitest'

import { parseTimeZone } from '@/lib/zoned-time'

import { placeOf } from './workshop-fields-schema'

describe('placeOf', () => {
  it.each([
    ['America/Chicago', 'Chicago'],
    ['Pacific/Pago_Pago', 'Pago Pago'],
    ['America/Argentina/Buenos_Aires', 'Buenos Aires'],
    ['UTC', 'UTC+00:00'],
    ['Etc/GMT+5', 'UTC-05:00'],
  ])('names %s as %s, with no time zone ID', (zone, place) => {
    expect(placeOf(parseTimeZone(zone))).toBe(place)
  })
})
