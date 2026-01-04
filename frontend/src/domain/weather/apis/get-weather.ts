import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import type { WeatherForecast } from '../types'
import { WeatherKeys } from './weather.keys'

/**
 * Fetch public weather forecast data
 */
export async function getWeatherForecasts(): Promise<WeatherForecast[]> {
  const response = await apiClient.get<WeatherForecast[]>('/api/v1/weather')
  return response.data
}

/**
 * Hook for fetching public weather forecast data
 */
export function useWeatherForecast() {
  return useQuery({
    queryKey: WeatherKeys.list(),
    queryFn: getWeatherForecasts,
  })
}
