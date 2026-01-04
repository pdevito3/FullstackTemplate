import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import type { SecureWeatherForecast } from '../types'
import { WeatherKeys } from './weather.keys'

/**
 * Fetch secure weather forecast data (requires authentication)
 */
export async function getSecureWeatherForecasts(): Promise<SecureWeatherForecast[]> {
  const response = await apiClient.get<SecureWeatherForecast[]>('/api/v1/weather/secure')
  return response.data
}

/**
 * Hook for fetching secure weather forecast data (requires authentication)
 */
export function useSecureWeatherForecast(enabled: boolean = true) {
  return useQuery({
    queryKey: WeatherKeys.secure(),
    queryFn: getSecureWeatherForecasts,
    enabled,
    retry: false, // Don't retry on 401
  })
}
