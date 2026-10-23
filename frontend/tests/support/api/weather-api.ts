import type { SecureWeatherForecast } from '@/api/generated/models/types/secure-weather-forecast'
import type { WeatherForecast } from '@/api/generated/models/types/weather-forecast'
import type { ProblemDetails } from '@/lib/problem-details'
import type { MockRoute } from './mock-route'

interface Buildable<T> {
  build(): T
}

export const weatherPaths = {
  list: '/api/v1/weather',
  secureList: '/api/v1/weather/secure',
}

export const weatherApi = {
  listReturns(...forecasts: Buildable<WeatherForecast>[]): MockRoute {
    return { method: 'get', path: weatherPaths.list, status: 200, body: forecasts.map((f) => f.build()) }
  },

  listFails(problem: Buildable<ProblemDetails>): MockRoute {
    const body = problem.build()
    return { method: 'get', path: weatherPaths.list, status: body.status ?? 500, body }
  },

  secureListReturns(...forecasts: Buildable<SecureWeatherForecast>[]): MockRoute {
    return { method: 'get', path: weatherPaths.secureList, status: 200, body: forecasts.map((f) => f.build()) }
  },
}
