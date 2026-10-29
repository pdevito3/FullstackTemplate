import { queryOptions, useQuery } from '@tanstack/react-query'

import { getWorkshop } from '@/api/generated/workshops/clients/get-workshop'

import { WorkshopKeys } from './workshop.keys'

export const workshopQueryOptions = (id: string) =>
  queryOptions({
    queryKey: WorkshopKeys.detail(id),
    queryFn: ({ signal }) => getWorkshop({ path: { id }, signal }),
  })

export function useWorkshop(id: string) {
  return useQuery(workshopQueryOptions(id))
}
