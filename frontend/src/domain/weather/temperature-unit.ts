import { useState } from 'react'

export const TEMPERATURE_UNITS = ['fahrenheit', 'celsius'] as const
export type TemperatureUnit = (typeof TEMPERATURE_UNITS)[number]

const STORAGE_KEY = 'weather-temperature-unit'

function isTemperatureUnit(value: unknown): value is TemperatureUnit {
  return TEMPERATURE_UNITS.some((unit) => unit === value)
}

export function parseTemperatureUnit(value: string | null): TemperatureUnit {
  return isTemperatureUnit(value) ? value : 'fahrenheit'
}

export function useTemperatureUnit() {
  const [unit, setUnit] = useState<TemperatureUnit>(() =>
    parseTemperatureUnit(localStorage.getItem(STORAGE_KEY)),
  )

  const changeUnit = (next: TemperatureUnit) => {
    localStorage.setItem(STORAGE_KEY, next)
    setUnit(next)
  }

  return [unit, changeUnit] as const
}
