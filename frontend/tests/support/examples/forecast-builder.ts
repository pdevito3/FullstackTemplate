import type { SecureWeatherForecast } from '@/api/generated/models/types/secure-weather-forecast'
import type { WeatherForecast } from '@/api/generated/models/types/weather-forecast'

// A counter, not a random source. In one worker, the same tests in the same order give the same forecasts.
let seed = 0
const next = () => ++seed

// The nth day after 2026-01-01, as an ISO date.
const day = (n: number) => new Date(Date.UTC(2026, 0, 1 + n)).toISOString().slice(0, 10)

// The rule of WeatherForecast.TemperatureF in the Server. The builder derives it, so the two values cannot disagree.
const fahrenheit = (celsius: number) => 32 + Math.trunc(celsius / 0.5556)

class ForecastBuilder<T extends WeatherForecast> {
  private readonly forecast: T

  private constructor(forecast: T) {
    this.forecast = forecast
  }

  static create() {
    const n = next()
    return new ForecastBuilder<WeatherForecast>({
      date: day(n),
      temperatureC: 20,
      temperatureF: fahrenheit(20),
      summary: 'Mild',
    })
  }

  private with(patch: Partial<WeatherForecast>) {
    return new ForecastBuilder<T>({ ...this.forecast, ...patch })
  }

  /** An ISO date, for example `2026-03-14`. */
  on(date: string) {
    return this.with({ date })
  }
  summarized(summary: string) {
    return this.with({ summary })
  }
  at(celsius: number) {
    return this.with({ temperatureC: celsius, temperatureF: fahrenheit(celsius) })
  }

  /** The forecast of the secure endpoint, which names the user who asked for it. */
  requestedBy(name: string) {
    return new ForecastBuilder<SecureWeatherForecast>({ ...this.forecast, requestedBy: name })
  }

  build(): T {
    return this.forecast
  }
}

export const aForecast = () => ForecastBuilder.create()
