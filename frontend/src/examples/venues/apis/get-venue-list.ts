import { keepPreviousData, useInfiniteQuery } from '@tanstack/react-query'

import type { VenueDto } from '@/api/generated/models/types/venue-dto'
import { getVenueList } from '@/api/generated/venues/clients/get-venue-list'
import { parsePagination, uniqueById } from '@/lib/pagination'
import { containsText } from '@/lib/querykit-text'

import { VenueKeys } from './venue.keys'

const VENUE_PAGE_SIZE = 10

export function useVenueSearch(search: string) {
  return useInfiniteQuery({
    queryKey: VenueKeys.list(search),
    queryFn: async ({ pageParam, signal }) => {
      const result = await getVenueList({
        query: { filters: containsText<VenueDto>('name', search), pageNumber: pageParam, pageSize: VENUE_PAGE_SIZE },
        signal,
        // The list says that a next page failed, and how to ask for it again.
        options: { suppressErrorToast: pageParam > 1 },
      })
      return { venues: result.data, pagination: parsePagination(result) }
    },
    placeholderData: keepPreviousData,
    initialPageParam: 1,
    getNextPageParam: ({ pagination }) => (pagination.hasNext ? pagination.pageNumber + 1 : undefined),
    select: ({ pages }) => uniqueById(pages.flatMap((page) => page.venues)),
  })
}
