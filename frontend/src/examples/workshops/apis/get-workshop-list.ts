import { keepPreviousData, useQuery } from '@tanstack/react-query'

import { getWorkshopList } from '@/api/generated/workshops/clients/get-workshop-list'
import { parsePagination } from '@/lib/pagination'

import { WorkshopKeys } from './workshop.keys'

export const WORKSHOP_PAGE_SIZE = 10

export interface WorkshopListParams {
  search: string
  page: number
}

export function useWorkshopList(params: WorkshopListParams, { enabled = true }: { enabled?: boolean } = {}) {
  return useQuery({
    enabled,
    queryKey: WorkshopKeys.list(params),
    queryFn: async ({ signal }) => {
      const result = await getWorkshopList({
        query: { search: params.search || undefined, pageNumber: params.page, pageSize: WORKSHOP_PAGE_SIZE },
        signal,
      })
      return { workshops: result.data, pagination: parsePagination(result) }
    },
    placeholderData: keepPreviousData,
  })
}
