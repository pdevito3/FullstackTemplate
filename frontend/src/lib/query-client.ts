import { QueryClient } from '@tanstack/react-query'

export const QUERY_RETRIES = 1

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        refetchOnWindowFocus: false,
        retry: QUERY_RETRIES,
      },
    },
  })
}
