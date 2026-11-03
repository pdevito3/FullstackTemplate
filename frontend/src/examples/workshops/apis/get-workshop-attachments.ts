import { queryOptions, useQuery } from '@tanstack/react-query'

import type { WorkshopAttachmentDto } from '@/api/generated/models/types/workshop-attachment-dto'
import { getWorkshopAttachments } from '@/api/generated/workshops/clients/get-workshop-attachments'

import { WorkshopKeys } from './workshop.keys'

// The list loads again this long before its URLs expire, so a link on the screen still works when the user clicks it
const REFRESH_MARGIN_MS = 60_000

// TanStack Query adds the stale time to the time of the load, but it starts the refetch interval at each subscribe
// and each update. So the stale time counts from the load, and the interval counts from now.
export function untilRefresh(attachments: WorkshopAttachmentDto[] | undefined, loadedAt: number): number | false {
  if (!attachments?.length) {
    return false
  }
  const earliest = Math.min(...attachments.map((a) => Date.parse(a.urlsExpireAt)))
  return Math.max(earliest - REFRESH_MARGIN_MS - loadedAt, 1_000)
}

export const workshopAttachmentsQueryOptions = (workshopId: string) =>
  queryOptions({
    queryKey: WorkshopKeys.attachments(workshopId),
    queryFn: ({ signal }) => getWorkshopAttachments({ path: { id: workshopId }, signal }),
    // The interval loads the list on an open tab. After the computer sleeps, the focus loads it, because it is stale.
    staleTime: (query) => untilRefresh(query.state.data, query.state.dataUpdatedAt) || 0,
    refetchInterval: (query) => untilRefresh(query.state.data, Date.now()),
    refetchOnWindowFocus: true,
  })

export function useWorkshopAttachments(workshopId: string) {
  return useQuery(workshopAttachmentsQueryOptions(workshopId))
}
