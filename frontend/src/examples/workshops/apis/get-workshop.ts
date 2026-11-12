import { queryOptions, useQuery } from '@tanstack/react-query'

import { getWorkshop } from '@/api/generated/workshops/clients/get-workshop'

import { WorkshopKeys } from './workshop.keys'

export const workshopQueryOptions = (id: string) =>
  queryOptions({
    queryKey: WorkshopKeys.detail(id),
    // The pages of the workshop show each error in their alert, so the error shows no toast (messages.md, "In the SPA").
    queryFn: ({ signal }) => getWorkshop({ path: { id }, signal, options: { suppressErrorToast: true } }),
  })

export function useWorkshop(id: string) {
  return useQuery(workshopQueryOptions(id))
}
