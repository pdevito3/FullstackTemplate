/**
 * Weather domain types
 */

export interface WeatherForecast {
  date: string
  temperatureC: number
  temperatureF: number
  summary: string
}

export interface SecureWeatherForecast extends WeatherForecast {
  requestedBy: string
}
