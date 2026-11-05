import { keepPreviousData, useInfiniteQuery, type InfiniteData } from '@tanstack/react-query'

import type { VenueDto } from '@/api/generated/models/types/venue-dto'
import { getVenueList } from '@/api/generated/venues/clients/get-venue-list'
import { parsePagination, uniqueById } from '@/lib/pagination'
import { containsText } from '@/lib/querykit-text'

import { VenueKeys } from './venue.keys'

const VENUE_PAGE_SIZE = 10

export function useVenueSearch(search: string) {
  return useInfiniteQuery({
    queryKey: VenueKeys.list(search),
    queryFn: async ({ client, queryKey, pageParam, signal }) => {
      // The list says that a next page failed, and how to ask for it again. A page that the list already has can fail
      // only when the list loads again. The list then has no message, so that failure keeps its toast.
      const isNextPage = pageParam > 1 && !client.getQueryData<InfiniteData<unknown, number>>(queryKey)?.pageParams.includes(pageParam)
      const result = await getVenueList({
        query: { filters: containsText<VenueDto>('name', search), pageNumber: pageParam, pageSize: VENUE_PAGE_SIZE },
        signal,
        options: { suppressErrorToast: isNextPage },
      })
      return { venues: result.data, pagination: parsePagination(result) }
    },
    placeholderData: keepPreviousData,
    initialPageParam: 1,
    getNextPageParam: ({ pagination }) => (pagination.hasNext ? pagination.pageNumber + 1 : undefined),
    select: ({ pages }) => uniqueById(pages.flatMap((page) => page.venues)),
  })
}
