import { queryOptions, useQuery } from '@tanstack/react-query'

import { getWorkshopAttachments } from '@/api/generated/workshops/clients/get-workshop-attachments'
import { refreshBeforeUrlsExpire } from '@/lib/attachment-list-refresh'

import { WorkshopKeys } from './workshop.keys'

export const workshopAttachmentsQueryOptions = (workshopId: string) =>
  queryOptions({
    queryKey: WorkshopKeys.attachments(workshopId),
    queryFn: ({ signal }) => getWorkshopAttachments({ path: { id: workshopId }, signal }),
    ...refreshBeforeUrlsExpire,
  })

export function useWorkshopAttachments(workshopId: string) {
  return useQuery(workshopAttachmentsQueryOptions(workshopId))
}
