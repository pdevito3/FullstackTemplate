import { useQuery } from '@tanstack/react-query'
import { getWeatherForecast } from '@/api/generated/weather/clients/get-weather-forecast'
import { WeatherKeys } from './weather.keys'

/**
 * Hook for fetching public weather forecast data
 */
export function useWeatherForecast() {
  return useQuery({
    queryKey: WeatherKeys.list(),
    queryFn: ({ signal }) => getWeatherForecast({ signal }),
  })
}
