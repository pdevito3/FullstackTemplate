import { describe, expect, it } from 'vitest'

import { TEMPERATURE_UNITS, parseTemperatureUnit } from './temperature-unit'

describe('parseTemperatureUnit', () => {
  it.each(TEMPERATURE_UNITS)('keeps the stored unit %s', (unit) => {
    expect(parseTemperatureUnit(unit)).toBe(unit)
  })

  it.each([null, '', 'kelvin', 'Celsius'])('falls back to fahrenheit for %j', (value) => {
    expect(parseTemperatureUnit(value)).toBe('fahrenheit')
  })
})
