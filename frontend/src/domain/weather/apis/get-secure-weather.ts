import { useQuery } from '@tanstack/react-query'

import { getSecureWeatherForecast } from '@/api/generated/weather/clients/get-secure-weather-forecast'

import { WeatherKeys } from './weather.keys'

/**
 * Hook for fetching secure weather forecast data (requires authentication)
 */
export function useSecureWeatherForecast(enabled: boolean = true) {
  return useQuery({
    queryKey: WeatherKeys.secure(),
    queryFn: ({ signal }) => getSecureWeatherForecast({ signal }),
    enabled,
    retry: false, // Don't retry on 401
  })
}
