/**
 * Query Key Factory for Weather domain
 *
 * Provides type-safe, hierarchical query keys for TanStack Query.
 * Keys are structured from generic to specific for granular cache invalidation.
 *
 * @example
 * // Invalidate all weather queries
 * queryClient.invalidateQueries({ queryKey: WeatherKeys.all })
 *
 * // Invalidate only the weather list
 * queryClient.invalidateQueries({ queryKey: WeatherKeys.list() })
 */
export const WeatherKeys = {
  all: ['weather'] as const,
  list: () => [...WeatherKeys.all, 'list'] as const,
  secure: () => [...WeatherKeys.all, 'secure'] as const,
}
