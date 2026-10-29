import { keepPreviousData, useInfiniteQuery } from '@tanstack/react-query'

import { getVenueList } from '@/api/generated/venues/clients/get-venue-list'
import { parsePagination } from '@/lib/pagination'

import { VenueKeys } from './venue.keys'

const VENUE_PAGE_SIZE = 10

/** The venues whose name contains `search`, one page at a time. */
export function useVenueSearch(search: string) {
  return useInfiniteQuery({
    queryKey: VenueKeys.list(search),
    queryFn: async ({ pageParam, signal }) => {
      const result = await getVenueList({
        query: { search: search || undefined, pageNumber: pageParam, pageSize: VENUE_PAGE_SIZE },
        signal,
      })
      return { venues: result.data, pagination: parsePagination(result) }
    },
    placeholderData: keepPreviousData,
    initialPageParam: 1,
    getNextPageParam: ({ pagination }) => (pagination.hasNext ? pagination.pageNumber + 1 : undefined),
  })
}
